using Backend.Modules.Projects.Models;
using Backend.Modules.Projects.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Backend.Modules.Projects.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly ProjectsService _projectsService;

    public ProjectsController(ProjectsService projectsService)
    {
        _projectsService = projectsService;
    }

    // GET api/projects
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var projects = await _projectsService.GetAllAsync();
        return Ok(projects);
    }

    // GET api/projects/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var project = await _projectsService.GetByIdAsync(id);
        if (project == null)
            return NotFound(new { message = "Projet introuvable" });

        return Ok(project);
    }

    // POST api/projects
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request)
    {
        var project = await _projectsService.CreateAsync(
            request.Name,
            request.Description,
            request.PortfolioDirectorId
        );

        return Ok(new
        {
            message = "Projet créé avec succès",
            data = project
        });
    }

    // PATCH api/projects/{id}/assign
    [HttpPatch("{id:guid}/assign")]
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