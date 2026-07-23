using Backend.Data;
using Backend.Modules.Events.Models;
using Backend.Modules.Projects.Models;
using Microsoft.EntityFrameworkCore;
using Backend.Modules.Tasks.Models;

namespace Backend.Modules.Events.Handlers;

public class CreateTasksFromStepsHandler: IActionHandler
{
    public string ActionType=>"CREATE_TASKS_FROM_STEPS";
    private readonly AppDbContext _db;
    private readonly ILogger<CreateTasksFromStepsHandler> _logger;

    public CreateTasksFromStepsHandler(AppDbContext db, ILogger<CreateTasksFromStepsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task HandleAsync(WorkflowRule rule, AcpEventDto eventDto, Guid? projectId)
    {
        var streamId = eventDto.StreamId;

        if (streamId == null || projectId == null)
        {
            _logger.LogWarning("CreateTasksFromStepsHandler: streamId or projectId is null");
            return;
        }

        var teamType = eventDto.LeadRole == "BusinessTeamLead"
            ? TeamType.Business
            : TeamType.Technical;

        var steps = await _db.ProjectSteps
            .Where(s => s.StreamId == streamId)
            .ToListAsync();

        if (!steps.Any())
        {
            _logger.LogWarning("No steps found for stream {StreamId}", streamId);
            return;
        }

        var stream = await _db.Streams.FindAsync(streamId);

        foreach (var step in steps)
        {
            var stepTeamType = step.TeamType ?? teamType;

            var assignedKeycloakId = await FindBestConsultantKeycloakIdAsync(
                projectId.Value, streamId.Value, stepTeamType);

            if (assignedKeycloakId == null)
            {
                _logger.LogWarning("No consultant found for step {StepName} teamType {TeamType}",
                    step.StepName, stepTeamType);
                continue;
            }

            _db.AcpTasks.Add(new AcpTask
            {
                Title = step.StepName,
                ToolName = step.ToolName,
                AssignedTo = assignedKeycloakId,
                StreamId = stream?.Id,
                ProjectId = projectId.Value,
                StepId = step.Id,
                Status = 0
            });

            _logger.LogInformation("Tâche créée : {StepName} → {Consultant} (TeamType: {TeamType})",
                step.StepName, assignedKeycloakId, stepTeamType);
        }

        await _db.SaveChangesAsync();
    }

    private async Task<string?> FindBestConsultantKeycloakIdAsync(
        Guid projectId, Guid streamId, TeamType teamType)
    {
        var memberIds = await _db.StreamMembers
            .Where(m => m.StreamId == streamId && m.TeamType == teamType)
            .Select(m => m.ConsultantId)
            .ToListAsync();

        if (!memberIds.Any())
            return null;

        var bestConsultant = await _db.Users
            .Where(u => memberIds.Contains(u.Id))
            .Select(u => new
            {
                u.KeycloakId,
                ActiveTasks = _db.AcpTasks.Count(t =>
                    t.AssignedTo == u.KeycloakId && t.Status != AcpTaskStatus.Done)
            })
            .OrderBy(u => u.ActiveTasks)
            .FirstOrDefaultAsync();

        return bestConsultant?.KeycloakId;
    }
}