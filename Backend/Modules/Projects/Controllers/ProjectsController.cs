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



namespace Backend.Modules.Projects.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly ProjectsService _projectsService;
    private readonly AppDbContext _db;
    private readonly EventPublisher _eventPublisher;
    private readonly StreamingSubscriptionService _streamingService;

    public ProjectsController(ProjectsService projectsService, AppDbContext db, EventPublisher eventPublisher, StreamingSubscriptionService streamingService)
    {
        _projectsService = projectsService;
        _db=db;
        _eventPublisher=eventPublisher;
        _streamingService = streamingService;
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
            request.PortfolioId   
        );

        await _streamingService.SubscribeToProjectAsync(request.Name);
        var safeName = request.Name.ToLower().Replace(" ", "-");
        await _streamingService.WaitForTopicAsync($"project.{safeName}");

        await _eventPublisher.PublishAsync(new
        {
            eventType = "ProjetCréé",
            directorId = portfolio.PortfolioDirectorId,   
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


}

public class CreateProjectRequest
{
    [Required(ErrorMessage = "Name est obligatoire")]
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    [Required(ErrorMessage = "PortfolioId est obligatoire")]
    public Guid PortfolioId { get; set; }

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