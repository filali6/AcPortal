using Backend.Data;
using Backend.Modules.Events.Models;
using Backend.Modules.Tasks.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Backend.Hubs;

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

    public async Task HandleAsync(
        WorkflowRule rule,
        AcpEventDto eventDto,
        Guid? projectId)
    {
        if (projectId == null) return;

        var query = _db.ProjectSteps
            .Where(s => s.ProjectId == projectId);

        // filtre par stream si fourni
        if (eventDto.StreamId.HasValue)
            query = query.Where(s => s.StreamId == eventDto.StreamId);

        var steps = await query
            .OrderBy(s => s.Order)
            .ToListAsync();

        foreach (var step in steps)
        {
            var taskExists = await _db.AcpTasks
                .AnyAsync(t => t.StepId == step.Id);
            if (taskExists) continue;

            var status = step.DependsOnStepId.HasValue
                ? AcpTaskStatus.Blocked
                : AcpTaskStatus.Pending;

            var assignedTo = await FindBestConsultantAsync(
                projectId, eventDto.StreamId);

            var task = new AcpTask
            {
                Title = step.StepName,
                Description = $"Tâche depuis step : {step.StepName}",
                ToolName = step.ToolName,
                AssignedTo = assignedTo ?? "Non assigné",
                Status = status,
                CreatedAt = DateTime.UtcNow,
                ProjectId = projectId,
                StepId = step.Id
            };

            _db.AcpTasks.Add(task);

            if (assignedTo != null)
            {
                var consultant = await _db.Users
                    .FirstOrDefaultAsync(u => u.FullName == assignedTo);
                if (consultant != null)
                {
                    await _hubContext.Clients
                        .Group(consultant.Id.ToString())
                        .SendAsync("NewNotification", new
                        {
                            message = $"Nouvelle tâche : {step.StepName}",
                            projectId = projectId
                        });
                }
            }
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Tâches créées depuis les steps — projet {ProjectId}",
            projectId);
    }

    private async Task<string?> FindBestConsultantAsync(
        Guid? projectId,
        Guid? streamId = null)
    {
        List<Guid> memberIds;

        if (streamId.HasValue)
        {
            memberIds = await _db.StreamMembers
                .Where(m => m.StreamId == streamId)
                .Select(m => m.ConsultantId)
                .ToListAsync();
        }
        else
        {
            var team = await _db.Teams
                .FirstOrDefaultAsync(t => t.ProjectId == projectId);
            if (team == null) return null;

            memberIds = await _db.TeamMembers
                .Where(m => m.TeamId == team.Id
                         && m.ConsultantId != team.ChefEquipeId)
                .Select(m => m.ConsultantId)
                .ToListAsync();
        }

        if (!memberIds.Any()) return null;

        string? bestName = null;
        int minTasks = int.MaxValue;

        foreach (var memberId in memberIds)
        {
            var consultant = await _db.Users.FindAsync(memberId);
            if (consultant == null) continue;

            var count = await _db.AcpTasks
                .CountAsync(t =>
                    t.AssignedTo == consultant.FullName
                    && t.ProjectId == projectId
                    && t.Status != AcpTaskStatus.Done);

            if (count < minTasks)
            {
                minTasks = count;
                bestName = consultant.FullName;
            }
        }

        return bestName;
    }
}