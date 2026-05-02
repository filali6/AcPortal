using Backend.Modules.Projects.Models;
using Backend.Modules.Projects.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Backend.Data;
using System.Text.Json;
using Backend.Modules.Events.Services;
using Backend.Modules.Events.Models;
using Microsoft.EntityFrameworkCore;
using Backend.Modules.Contracts.Services;
using Backend.Modules.Tasks.Models;



namespace Backend.Modules.Projects.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly ProjectsService _projectsService;
    private readonly AppDbContext _db;
    private readonly EventPublisher _eventPublisher;
    private readonly StreamingSubscriptionService _streamingService;
    private readonly ContractsService _contractsService;
    private readonly ILogger<ProjectsController> _logger;


    public ProjectsController(ProjectsService projectsService, AppDbContext db, EventPublisher eventPublisher, StreamingSubscriptionService streamingService, ContractsService contractsService, ILogger<ProjectsController> logger)
    {
        _projectsService = projectsService;
        _db=db;
        _eventPublisher=eventPublisher;
        _streamingService = streamingService;
        _contractsService = contractsService;
        _logger = logger;

    }

    
    [HttpGet]
    [Authorize(Roles = "HeadOfCDS,PortfolioDirector,Consultant,ProjectManager,BusinessTeamLead,TechnicalTeamLead,Consultant")]
    public async Task<IActionResult> GetAll()
    {
        var projects = await _projectsService.GetAllAsync();
        return Ok(projects);
    }

  
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "HeadOfCDS,PortfolioDirector,ProjectManager,BusinessTeamLead,TechnicalTeamLead")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var project = await _projectsService.GetByIdAsync(id);
        if (project == null)
            return NotFound(new { message = "Projet introuvable" });

        return Ok(project);
    }


    [HttpPost]
    [Authorize(Roles = "HeadOfCDS")]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request)
    {
        // Vérifier que le portfolio existe et récupérer le PD
        var portfolio = await _db.Portfolios
            .Include(p => p.PortfolioDirector)
            .FirstOrDefaultAsync(p => p.Id == request.PortfolioId);

        if (portfolio == null)
            return BadRequest(new { message = "Portfolio introuvable" });

        var project = await _projectsService.CreateAsync(
            request.Name,
            request.Description,
            request.PortfolioId , request.TargetDate   
        );
        if (request.ContractId.HasValue)
        {
            await _contractsService.LinkProjectAsync(request.ContractId.Value, project.Id);
        }
        _logger.LogInformation("ContractId reçu: {ContractId}", request.ContractId);

        await _streamingService.SubscribeToProjectAsync(request.Name);
        var safeName = request.Name.ToLower().Replace(" ", "-");
        await _streamingService.WaitForTopicAsync($"project.{safeName}");

        await _eventPublisher.PublishAsync(new
        {
            eventType = "ProjetCréé",
            directorId = portfolio.PortfolioDirectorId,
            projectName = project.Name,
            projectId = project.Id
        }, project.Id, request.Name);

        return Ok(new
        {
            message = "Projet créé avec succès",
            data = project
        });
    }
    [HttpPatch("{id}/assign-manager")]
    [Authorize(Roles = "PortfolioDirector")]
    public async Task<IActionResult> AssignManager(
    Guid id,
    [FromBody] AssignManagerDto dto)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return NotFound();

        project.ProjectManagerId = dto.ProjectManagerId;
        await _db.SaveChangesAsync();

        // publier event → tâche créée pour le Project Manager
        await _eventPublisher.PublishAsync(new
        {
            eventType = "ProjectManagerAssigné",
            projectId = id,
            projectManagerId = dto.ProjectManagerId
        }, id, project.Name);
        return Ok(project);
    }


    // [HttpPatch("{id:guid}/assign")]
    // [Authorize(Roles = "HeadOfCDS")]
    // public async Task<IActionResult> AssignDirector(Guid id, [FromBody] AssignDirectorRequest request)
    // {
    //     var project = await _projectsService.AssignDirectorAsync(id, request.DirectorId);
    //     if (project == null)
    //         return NotFound(new { message = "Projet ou Director introuvable" });

    //     return Ok(new
    //     {
    //         message = "Director assigné avec succès",
    //         data = project
    //     });
    // }
    [HttpGet("my")]
    [Authorize(Roles ="PortfolioDirector")]
    public async Task<IActionResult> GetMyProjects()
    {
        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        if (user == null) return NotFound();

        var projects = await _projectsService.GetMyProjectsAsync(user.Id);
        return Ok(projects);
    }
    [HttpGet("managed")]
    [Authorize(Roles = "ProjectManager")]
    public async Task<IActionResult> GetManagedProjects()
    {
        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        if (user == null) return NotFound();

        var projects = await _db.Projects
            .Where(p => p.ProjectManagerId == user.Id)
            .ToListAsync();

        return Ok(projects);
    }
    [HttpPatch("{id:guid}")]
    [Authorize(Roles = "HeadOfCDS")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectRequest request)
    {
        var project = await _projectsService.UpdateAsync(id, request.Name, request.Description, request.TargetDate);
        if (project == null) return NotFound();
        return Ok(project);
    }

    [HttpGet("stats")]
    [Authorize(Roles = "HeadOfCDS")]
    public async Task<IActionResult> GetStats()
    {
        var total = await _db.Projects.CountAsync();
        var inProgress = await _db.Projects
            .Where(p => _db.Streams.Any(s => s.ProjectId == p.Id))
            .CountAsync();
        var contracts = await _db.Contracts.CountAsync();

        return Ok(new { total, inProgress, contracts });
    }

    [HttpGet("{id:guid}/details")]
    [Authorize(Roles = "HeadOfCDS,PortfolioDirector,ProjectManager")]
    public async Task<IActionResult> GetDetails(Guid id)
    {
        var project = await _db.Projects
            .Include(p => p.Portfolio)
                .ThenInclude(pf => pf!.PortfolioDirector)
            .Include(p => p.ProjectManager)
            .Include(p => p.Streams)
                .ThenInclude(s => s.Members)
                    .ThenInclude(m => m.Consultant)
            .Include(p => p.Streams)
                .ThenInclude(s => s.BusinessTeamLead)
            .Include(p => p.Streams)
                .ThenInclude(s => s.TechnicalTeamLead)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (project == null) return NotFound();

        var tasks = await _db.AcpTasks
            .Where(t => t.ProjectId == id)
            .ToListAsync();

        var steps = await _db.ProjectSteps
            .Where(s => s.ProjectId == id)
            .ToListAsync();

        var totalTasks = tasks.Count;
        var doneTasks = tasks.Count(t => t.Status == AcpTaskStatus.Done);
        var progress = totalTasks > 0 ? (int)Math.Round((double)doneTasks / totalTasks * 100) : 0;

        return Ok(new
        {
            project.Id,
            project.Name,
            project.Description,
            project.CreatedAt,
            project.TargetDate,
            portfolio = project.Portfolio == null ? null : new
            {
                project.Portfolio.Id,
                project.Portfolio.Name,
                director = project.Portfolio.PortfolioDirector == null ? null : new
                {
                    project.Portfolio.PortfolioDirector.Id,
                    project.Portfolio.PortfolioDirector.FullName
                }
            },
            projectManager = project.ProjectManager == null ? null : new
            {
                project.ProjectManager.Id,
                project.ProjectManager.FullName
            },
            progress,
            totalTasks,
            doneTasks,
            streams = project.Streams.Select(s => new
            {
                s.Id,
                s.Name,
                s.CreatedAt,
                businessTeamLead = s.BusinessTeamLead == null ? null : new
                {
                    s.BusinessTeamLead.Id,
                    s.BusinessTeamLead.FullName
                },
                technicalTeamLead = s.TechnicalTeamLead == null ? null : new
                {
                    s.TechnicalTeamLead.Id,
                    s.TechnicalTeamLead.FullName
                },
                members = s.Members.Select(m => new
                {
                    m.Id,
                    m.Consultant.FullName,
                    m.Consultant.Email,
                    teamType=m.TeamType.ToString()
                }),
                streamTasks = tasks.Where(t => t.StreamId == s.Id).Select(t => new
                {
                    t.Id,
                    t.Title,
                    t.Status,
                    t.AssignedTo
                }),
                streamProgress = tasks.Where(t => t.StreamId == s.Id).Count() > 0
                    ? (int)Math.Round((double)tasks.Count(t => t.StreamId == s.Id && t.Status == AcpTaskStatus.Done)
                        / tasks.Count(t => t.StreamId == s.Id) * 100)
                    : 0
            }),
            steps = steps.Select(s => new
            {
                s.Id,
                s.StepName,
                s.Order,
                s.DependsOnStepId,
                s.StreamId
            })
        });
    }




}

public class CreateProjectRequest
{
    [Required(ErrorMessage = "Name est obligatoire")]
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    [Required(ErrorMessage = "PortfolioId est obligatoire")]
    public Guid PortfolioId { get; set; }
    public DateTime? TargetDate { get; set; }
    public Guid? ContractId { get; set; }

}

// public class AssignDirectorRequest
// {
//     [Required(ErrorMessage = "DirectorId est obligatoire")]
//     public Guid DirectorId { get; set; }
// }
public class AssignManagerDto
{
    public Guid ProjectManagerId { get; set; }
}
public class UpdateProjectRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? TargetDate { get; set; }
}