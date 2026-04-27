using Backend.Data;
using Backend.Modules.Events.Models;
using Backend.Modules.Projects.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Backend.Modules.Projects.Controllers;

[ApiController]
[Route("api/steps")]
public class ProjectStepsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProjectStepsController(AppDbContext db)
    {
        _db = db;
    }

 
    [HttpPost]
    [Authorize(Roles = "BusinessTeamLead,TechnicalTeamLead,HeadOfCDS")]
    // public async Task<IActionResult> CreateSteps([FromBody] CreateStepsRequest request)
    // {

    //     foreach (var stepDto in request.Steps)
    //     {
    //         var step = new ProjectStep
    //         {
    //             ProjectId = request.ProjectId,
    //             StepName = stepDto.StepName,
    //             ToolName = stepDto.ToolName,
    //             Order = stepDto.Order,
    //             CanBeParallel = stepDto.CanBeParallel,
    //             DependsOnStepId = stepDto.DependsOnStepId,
    //             CreatedAt = DateTime.UtcNow
    //         };
    //         _db.ProjectSteps.Add(step);
    //     }


    //     var payload = JsonSerializer.Serialize(new
    //     {
    //         eventType = "StepsDéfinis",
    //         projectId = request.ProjectId
    //     });

    //     _db.OutboxMessages.Add(new OutboxMessage
    //     {
    //         Topic = $"project.{request.ProjectId}",
    //         Payload = payload,
    //         CreatedAt = DateTime.UtcNow,
    //         IsProcessed = false,
    //         Retries = 0
    //     });

    //     await _db.SaveChangesAsync();

    //     return Ok(new
    //     {
    //         message = "Steps créés avec succès — tâches en cours de génération",
    //         projectId = request.ProjectId,
    //         stepsCount = request.Steps.Count
    //     });
    // }
    public async Task<IActionResult> CreateSteps([FromBody] CreateStepsRequest request)
    {
        var createdSteps = new Dictionary<string, Guid>();

        foreach (var stepDto in request.Steps.OrderBy(s => s.Order))
        {
            Guid? resolvedDependsOn = null;
            if (!string.IsNullOrEmpty(stepDto.DependsOnStepId) && createdSteps.ContainsKey(stepDto.DependsOnStepId))
                resolvedDependsOn = createdSteps[stepDto.DependsOnStepId];

            var step = new ProjectStep
            {
                ProjectId = request.ProjectId,
                StepName = stepDto.StepName,
                ToolName = stepDto.ToolName,
                Order = stepDto.Order,
                CanBeParallel = stepDto.CanBeParallel,
                DependsOnStepId = resolvedDependsOn,
                StreamId = stepDto.StreamId,
                CreatedAt = DateTime.UtcNow
            };
            _db.ProjectSteps.Add(step);
            await _db.SaveChangesAsync();
            createdSteps[stepDto.StepName] = step.Id;
        }
        var streamId = request.Steps.FirstOrDefault()?.StreamId;
        var payload = JsonSerializer.Serialize(new
        {
            eventType = "StepsDéfinis",
            projectId = request.ProjectId,
            streamId = streamId
        });

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Topic = $"project.{request.ProjectId}",
            Payload = payload,
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            Retries = 0
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Steps créés avec succès — tâches en cours de génération",
            projectId = request.ProjectId,
            stepsCount = request.Steps.Count
        });
    }

    [HttpGet("project/{projectId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetByProject(Guid projectId)
    {
        var steps = _db.ProjectSteps
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.Order)
            .Select(s => new
            {
                s.Id,
                s.StepName,
                s.ToolName,
                s.Order,
                s.CanBeParallel,
                s.DependsOnStepId
            })
            .ToList();

        return Ok(steps);
    }
}

public class CreateStepsRequest
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    public List<StepDto> Steps { get; set; } = new();
}

