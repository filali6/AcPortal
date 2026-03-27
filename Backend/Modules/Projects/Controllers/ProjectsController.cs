using Backend.Modules.Projects.Models;
using Backend.Modules.Projects.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Backend.Data;


namespace Backend.Modules.Projects.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly ProjectsService _projectsService;
    private readonly AppDbContext _db;

    public ProjectsController(ProjectsService projectsService, AppDbContext db)
    {
        _projectsService = projectsService;
        _db=db;
    }

    
    [HttpGet]
    [Authorize(Roles ="SuperAdmin,PortfolioDirector,Consultant")]
    public async Task<IActionResult> GetAll()
    {
        var projects = await _projectsService.GetAllAsync();
        return Ok(projects);
    }

  
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,PortfolioDirector")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var project = await _projectsService.GetByIdAsync(id);
        if (project == null)
            return NotFound(new { message = "Projet introuvable" });

        return Ok(project);
    }

   
    [HttpPost]
    [Authorize(Roles="SuperAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request)
    {
        var project = await _projectsService.CreateAsync(
            request.Name,
            request.Description,
            request.PortfolioDirectorId
        );
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            eventType = "ProjetCréé",
            directorId = request.PortfolioDirectorId,
            projectId = project.Id
        });

        _db.OutboxMessages.Add(new Backend.Modules.Events.Models.OutboxMessage
        {
            Topic = $"project.{project.Id}",
            Payload = payload,
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            Retries = 0
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Projet créé avec succès",
            data = project
        });
    }

     
    [HttpPatch("{id:guid}/assign")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> AssignDirector(Guid id, [FromBody] AssignDirectorRequest request)
    {
        var project = await _projectsService.AssignDirectorAsync(id, request.DirectorId);
        if (project == null)
            return NotFound(new { message = "Projet ou Director introuvable" });

        return Ok(new
        {
            message = "Director assigné avec succès",
            data = project
        });
    }
    [HttpGet("my")]
    [Authorize(Roles ="PortfolioDirector")]
    public async Task<IActionResult> GetMyProjects()
    {
        var directorId=Guid.Parse(
            User.FindFirst("id")!.Value
        );
        var projects = await _projectsService.GetMyProjectsAsync(directorId);
        return Ok(projects);
    }
}

public class CreateProjectRequest
{
    [Required(ErrorMessage = "Name est obligatoire")]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "PortfolioDirectorId est obligatoire")]
    public Guid PortfolioDirectorId { get; set; }
}

public class AssignDirectorRequest
{
    [Required(ErrorMessage = "DirectorId est obligatoire")]
    public Guid DirectorId { get; set; }
}