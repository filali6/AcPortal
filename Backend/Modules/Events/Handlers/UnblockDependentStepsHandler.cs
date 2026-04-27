using Backend.Data;
using Backend.Modules.Events.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Backend.Hubs;
using Backend.Modules.Tasks.Models;

namespace Backend.Modules.Events.Handlers;

public class UnblockDependentStepsHandler : IActionHandler
{
    public string ActionType => "UNBLOCK_DEPENDENT_STEPS";

    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<UnblockDependentStepsHandler> _logger;

    public UnblockDependentStepsHandler(
        AppDbContext db,
        IHubContext<NotificationHub> hubContext,
        ILogger<UnblockDependentStepsHandler> logger)
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
        if (eventDto.StepId == null) return;

        var dependentSteps = await _db.ProjectSteps
            .Where(s => s.DependsOnStepId == eventDto.StepId)
            .ToListAsync();

        foreach (var depStep in dependentSteps)
        {
            var task = await _db.AcpTasks
                .FirstOrDefaultAsync(t => t.StepId == depStep.Id);

            if (task == null || task.Status != AcpTaskStatus.Blocked)
                continue;

            task.Status = AcpTaskStatus.Pending;
            task.UpdatedAt = DateTime.UtcNow;

            var consultant = await _db.Users
                .FirstOrDefaultAsync(u => u.FullName == task.AssignedTo);

            if (consultant != null)
            {
                await _hubContext.Clients
                    .Group(consultant.Id.ToString())
                    .SendAsync("NewNotification", new
                    {
                        message = $"Tâche débloquée : {task.Title}",
                        projectId = projectId
                    });
            }

            _logger.LogInformation(
                "Tâche débloquée : {Title}", task.Title);
        }

        await _db.SaveChangesAsync();
    }
}