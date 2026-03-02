using Backend.Modules.Tasks.Models;
using Backend.Modules.Tasks.Services;
using Microsoft.AspNetCore.Mvc;
using Backend.Modules.Tasks.Models;


namespace Backend.Modules.Tasks.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private readonly TasksService _tasksService;

    public TasksController(TasksService tasksService)
    {
        _tasksService = tasksService;
    }

    // GET api/tasks → liste toutes les tâches
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tasks = await _tasksService.GetAllAsync();
        return Ok(tasks);
    }

    // GET api/tasks/{id} → détail d'une tâche
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var task = await _tasksService.GetByIdAsync(id);
        if (task == null)
            return NotFound(new { message = "Tâche introuvable" });

        return Ok(task);
    }

    // PATCH api/tasks/{id}/status → changer le statut
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request)
    {
        var task = await _tasksService.UpdateStatusAsync(id, request.Status);
        if (task == null)
            return NotFound(new { message = "Tâche introuvable" });

        return Ok(task);
    }

    // PATCH api/tasks/{id}/assign → assigner à un utilisateur
    [HttpPatch("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignRequest request)
    {
        var task = await _tasksService.AssignAsync(id, request.AssignedTo);
        if (task == null)
            return NotFound(new { message = "Tâche introuvable" });

        return Ok(task);
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