using Backend.Modules.Tasks.Models;
using Backend.Modules.Tasks.Services;
using Microsoft.AspNetCore.Mvc;
using Backend.Modules.Events.Services;
using Microsoft.AspNetCore.Authorization;
using Backend.Data;
using Microsoft.EntityFrameworkCore;


namespace Backend.Modules.Tasks.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private readonly TasksService _tasksService;
    private readonly AppDbContext _db;
    private readonly EventPublisher _eventPublisher;


    public TasksController(TasksService tasksService, AppDbContext db, EventPublisher eventPublisher)
    {
        _tasksService = tasksService;
        _db=db;
        _eventPublisher=eventPublisher;
    }

   
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll()
    {
        var tasks = await _tasksService.GetAllAsync();
        return Ok(tasks);
    }

     
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var task = await _tasksService.GetByIdAsync(id);
        if (task == null)
            return NotFound(new { message = "Tâche introuvable" });

        return Ok(task);
    }

    
    [HttpPatch("{id:guid}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request)
    {
        var task = await _tasksService.UpdateStatusAsync(id, request.Status);
        if (task == null)
            return NotFound(new { message = "Tâche introuvable" });

        if (request.Status == AcpTaskStatus.Done && task.ProjectId.HasValue)
        {
            if (request.Status == AcpTaskStatus.Done && task.ProjectId.HasValue)
            {
                var project = await _db.Projects.FindAsync(task.ProjectId);
                await _eventPublisher.PublishAsync(new
                {
                    eventType = "TâcheTerminée",
                    taskId = task.Id,
                    stepId = task.StepId,
                    projectId = task.ProjectId,
                    projectName=project!.Name
                }, task.ProjectId,project?.Name);
            }
        }

        return Ok(task);
    }

     
    [HttpPatch("{id:guid}/assign")]
    [Authorize]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignRequest request)
    {
        var task = await _tasksService.AssignAsync(id, request.AssignedTo);
        if (task == null)
            return NotFound(new { message = "Tâche introuvable" });

        return Ok(task);
    }
    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMyTasks()
    {
        // ✅ Récupérer l'ID Keycloak depuis le token
        var keycloakId = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;

        // Chercher le user dans ta BDD par KeycloakId
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);

        if (user == null) return Ok(new List<object>());

        var tasks = await _tasksService.GetMyTasksAsync(user.KeycloakId);
        return Ok(tasks);
    }
}

public class UpdateStatusRequest
{
    public AcpTaskStatus Status { get; set; }
}

public class AssignRequest
{
    public string AssignedTo { get; set; } = string.Empty;
}