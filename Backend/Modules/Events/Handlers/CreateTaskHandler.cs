using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Events.Models;
using Backend.Modules.Tasks.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Backend.Hubs;

namespace Backend.Modules.Events.Handlers;

public class CreateTaskHandler : IActionHandler
{
    public string ActionType => "CREATE_TASK";

    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<CreateTaskHandler> _logger;

    public CreateTaskHandler(
        AppDbContext db,
        IHubContext<NotificationHub> hubContext,
        ILogger<CreateTaskHandler> logger)
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
        var targetUserId = await ResolveTargetAsync(rule, eventDto);

        if (targetUserId == null)
        {
            _logger.LogWarning(
                "Destinataire introuvable pour {EventType}",
                eventDto.EventType);
            return;
        }

        await CreateTaskAsync(
            rule.TaskTitle!,
            rule.TaskDescription!,
            targetUserId.Value,
            projectId);
    }

    private async Task<Guid?> ResolveTargetAsync(
        WorkflowRule rule,
        AcpEventDto eventDto)
    {
        switch (rule.TargetType)
        {
            case "ROLE":
                if (!Enum.TryParse<GlobalRole>(
                    rule.TargetValue, out var roleEnum))
                    return null;
                var user = await _db.Users
                    .FirstOrDefaultAsync(u => u.Role == roleEnum);
                return user?.Id;

            case "CONTEXT_USER":
                return rule.TargetValue switch
                {
                    "DirectorId" => eventDto.DirectorId,
                    "ProjectManagerId" => eventDto.ProjectManagerId,
                    "BusinessTeamLeadId" => eventDto.BusinessTeamLeadId,
                    "TechnicalTeamLeadId" => eventDto.TechnicalTeamLeadId,
                    _ => null
                };

            default:
                return null;
        }
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
            .Group(user.Id.ToString())
            .SendAsync("NewNotification", new
            {
                message = title,
                projectId = projectId
            });

        _logger.LogInformation(
            "Tâche créée : {Title} → {User}",
            title, user.FullName);
    }
}