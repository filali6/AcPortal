using Backend.Modules.Tasks.Models;
using Backend.Modules.Tasks.Services;
using Microsoft.AspNetCore.Mvc;
 
using Microsoft.AspNetCore.Authorization;
using Backend.Data;


namespace Backend.Modules.Tasks.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private readonly TasksService _tasksService;
    private readonly AppDbContext _db;


    public TasksController(TasksService tasksService, AppDbContext db)
    {
        _tasksService = tasksService;
        _db=db;
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
            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                eventType = "TâcheTerminée",
                taskId = task.Id,
                stepId = task.StepId,
                projectId = task.ProjectId
            });

            _db.OutboxMessages.Add(new Backend.Modules.Events.Models.OutboxMessage
            {
                Topic = $"project.{task.ProjectId}",
                Payload = payload,
                CreatedAt = DateTime.UtcNow,
                IsProcessed = false,
                Retries = 0
            });

            await _db.SaveChangesAsync();
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
        var consultantName = User.FindFirst(
        "name")!.Value;

        var tasks = await _tasksService.GetMyTasksAsync(consultantName);
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