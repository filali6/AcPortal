using Backend.Data;
using Backend.Modules.Events.Models;
using Backend.Modules.Tasks.Models;
using Microsoft.AspNetCore.SignalR;
using Backend.Hubs;

namespace Backend.Modules.Events.Handlers;

public class CreateTasksForLeadsHandler : IActionHandler
{
    public string ActionType => "CREATE_TASKS_FOR_LEADS";

    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<CreateTasksForLeadsHandler> _logger;

    public CreateTasksForLeadsHandler(
        AppDbContext db,
        IHubContext<NotificationHub> hubContext,
        ILogger<CreateTasksForLeadsHandler> logger)
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
        // crée une tâche pour le Business Lead
        if (eventDto.BusinessTeamLeadId.HasValue)
            await CreateTaskAsync(
                rule.TaskTitle!,
                rule.TaskDescription!,
                eventDto.BusinessTeamLeadId.Value,
                projectId);

        // crée une tâche pour le Technical Lead
        if (eventDto.TechnicalTeamLeadId.HasValue)
            await CreateTaskAsync(
                rule.TaskTitle!,
                rule.TaskDescription!,
                eventDto.TechnicalTeamLeadId.Value,
                projectId);
    }

    private async Task CreateTaskAsync(
        string title,
        string description,
        Guid userId,
        Guid? projectId)
    {
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;

        var task = new AcpTask
        {
            Title = title,
            Description = description,
            ToolName = "portal",
            AssignedTo = user.KeycloakId,
            Status = AcpTaskStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ProjectId = projectId
        };

        _db.AcpTasks.Add(task);
        await _db.SaveChangesAsync();

        await _hubContext.Clients
            .Group(user.KeycloakId)
            .SendAsync("NewNotification", new
            {
                message = title,
                projectId = projectId
            });

        _logger.LogInformation(
            "Tâche créée pour lead : {Title} → {User}",
            title, user.FullName);
    }
}