using Backend.Data;
using Backend.Modules.Events.Models;
using Backend.Modules.Tasks.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Backend.Hubs;
using Backend.Modules.Projects.Models;

namespace Backend.Modules.Events.Handlers;

public class CreateTasksFromStepsHandler : IActionHandler
{
    public string ActionType => "CREATE_TASKS_FROM_STEPS";

    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<CreateTasksFromStepsHandler> _logger;

    public CreateTasksFromStepsHandler(
        AppDbContext db,
        IHubContext<NotificationHub> hubContext,
        ILogger<CreateTasksFromStepsHandler> logger)
    {
        _db = db;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task HandleAsync(WorkflowRule rule, AcpEventDto eventDto, Guid? projectId)
    {
        if (projectId == null) return;

        var query = _db.ProjectSteps.Where(s => s.ProjectId == projectId);
        if (eventDto.StreamId.HasValue)
            query = query.Where(s => s.StreamId == eventDto.StreamId);

        var steps = await query.OrderBy(s => s.Order).ToListAsync();

        // Déterminer le TeamType selon le rôle du Lead
        var teamType = eventDto.LeadRole == "BusinessTeamLead"
            ? TeamType.Business
            : TeamType.Technical;

        foreach (var step in steps)
        {
            var taskExists = await _db.AcpTasks.AnyAsync(t => t.StepId == step.Id);
            if (taskExists) continue;

            var status = step.DependsOnStepId.HasValue ? AcpTaskStatus.Blocked : AcpTaskStatus.Pending;

            var assignedKeycloakId = await FindBestConsultantKeycloakIdAsync(
                projectId, eventDto.StreamId, teamType);

            var task = new AcpTask
            {
                Title = step.StepName,
                Description = $"Task from step: {step.StepName}",
                ToolName = step.ToolName,
                AssignedTo = assignedKeycloakId ?? "Unassigned",
                Status = status,
                CreatedAt = DateTime.UtcNow,
                ProjectId = projectId,
                StepId = step.Id
            };

            _db.AcpTasks.Add(task);

            if (assignedKeycloakId != null)
            {
                await _hubContext.Clients.Group(assignedKeycloakId)
                    .SendAsync("NewNotification", new
                    {
                        message = $"New task: {step.StepName}",
                        projectId = projectId
                    });
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task<string?> FindBestConsultantKeycloakIdAsync(
        Guid? projectId, Guid? streamId, TeamType teamType)
    {
        List<Guid> memberIds;

        if (streamId.HasValue)
        {
            memberIds = await _db.StreamMembers
                .Where(m => m.StreamId == streamId && m.TeamType == teamType)
                .Select(m => m.ConsultantId)
                .ToListAsync();
        }
        else
        {
            memberIds = await _db.StreamMembers
                .Where(m => _db.Streams.Any(s => s.ProjectId == projectId && s.Id == m.StreamId)
                    && m.TeamType == teamType)
                .Select(m => m.ConsultantId)
                .ToListAsync();
        }

        // Si pas de membres dans cette équipe → prendre le Lead lui-même
        if (!memberIds.Any())
        {
            var stream = await _db.Streams
                .FirstOrDefaultAsync(s => s.Id == streamId);

            if (stream != null)
            {
                var leadId = teamType == TeamType.Business
                    ? stream.BusinessTeamLeadId
                    : stream.TechnicalTeamLeadId;

                if (leadId.HasValue)
                {
                    var lead = await _db.Users.FindAsync(leadId.Value);
                    return lead?.KeycloakId;
                }
            }
            return null;
        }

        // Assigner au consultant avec le moins de tâches
        string? bestKeycloakId = null;
        int minTasks = int.MaxValue;

        foreach (var memberId in memberIds)
        {
            var consultant = await _db.Users.FindAsync(memberId);
            if (consultant == null) continue;

            var count = await _db.AcpTasks.CountAsync(t =>
                t.AssignedTo == consultant.KeycloakId &&
                t.ProjectId == projectId &&
                t.Status != AcpTaskStatus.Done);

            if (count < minTasks)
            {
                minTasks = count;
                bestKeycloakId = consultant.KeycloakId;
            }
        }

        return bestKeycloakId;
    }
}