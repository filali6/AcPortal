using Backend.Data;
using Backend.Modules.Events.Models;
using Backend.Modules.Projects.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Backend.Modules.Events.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Projects.Controllers;

[ApiController]
[Route("api/steps")]
public class ProjectStepsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EventPublisher _eventPublisher;

    public ProjectStepsController(AppDbContext db,EventPublisher eventPublisher)
    {
        _db = db;
        _eventPublisher=eventPublisher;
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

        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var leadUser = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        var leadRole = leadUser?.Role.ToString() ?? "";
        var streamId = request.Steps.FirstOrDefault()?.StreamId;
        var project = await _db.Projects.FindAsync(request.ProjectId);
        await _eventPublisher.PublishAsync(new
        {
            eventType = "StepsDéfinis",
            projectId = request.ProjectId,
            projectName = project!.Name,
            streamId = streamId,
            leadRole = leadRole
        }, request.ProjectId, project.Name);

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
    [HttpGet("stream/{streamId:guid}/ai-steps")]
    [Authorize(Roles = "BusinessTeamLead,TechnicalTeamLead,HeadOfCDS")]
    public async Task<IActionResult> GetAiSteps(Guid streamId)
    {
        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var leadUser = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        var stream = await _db.Streams.FindAsync(streamId);
        if (stream == null) return NotFound();

        // Détermine le TeamType selon quel lead est connecté
        var teamType = stream.BusinessTeamLeadId == leadUser?.Id
            ? TeamType.Business
            : TeamType.Technical;

        var steps = await _db.ProjectSteps
            .Where(s => s.StreamId == streamId && s.TeamType == teamType)
            .OrderBy(s => s.Order)
            .Select(s => new { s.Id, s.StepName, s.ToolName, s.Order })
            .ToListAsync();

        return Ok(steps);
    }

    [HttpPost("stream/{streamId:guid}/approve")]
    [Authorize(Roles = "BusinessTeamLead,TechnicalTeamLead,HeadOfCDS")]
    public async Task<IActionResult> ApproveAiSteps(Guid streamId, [FromBody] ApproveAiStepsRequest request)
    {
        var stream = await _db.Streams.FindAsync(streamId);
        if (stream == null) return NotFound();

        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var leadUser = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        var project = await _db.Projects.FindAsync(stream.ProjectId);
        var teamType = stream.BusinessTeamLeadId == leadUser?.Id
            ? TeamType.Business
            : TeamType.Technical;

        if (request.Steps != null && request.Steps.Any())
        {
            var existing = _db.ProjectSteps.Where(s => s.StreamId == streamId && s.TeamType == teamType);
            _db.ProjectSteps.RemoveRange(existing);
            await _db.SaveChangesAsync();

            foreach (var stepDto in request.Steps.OrderBy(s => s.Order))
            {
                _db.ProjectSteps.Add(new ProjectStep
                {
                    ProjectId = stream.ProjectId,
                    StreamId = streamId,
                    StepName = stepDto.StepName,
                    ToolName = stepDto.ToolName,
                    Order = stepDto.Order,
                    CanBeParallel = false,
                    TeamType = teamType
                });
            }
            await _db.SaveChangesAsync();
        }

        await _eventPublisher.PublishAsync(new
        {
            eventType = "StepsDéfinis",
            projectId = stream.ProjectId,
            projectName = project!.Name,
            streamId = streamId,
            leadRole = teamType == TeamType.Business ? "BusinessTeamLead" : "TechnicalTeamLead"
        }, stream.ProjectId, project.Name);

        return Ok(new { message = "Steps approved" });
    }
    public class ApproveAiStepsRequest
    {
        public List<StepDto>? Steps { get; set; }
    }
}

public class CreateStepsRequest
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    public List<StepDto> Steps { get; set; } = new();
}

