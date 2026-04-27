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
        // resolve ALL targets from the rule
        var targetUserIds = await ResolveTargetsAsync(rule, eventDto);

        if (!targetUserIds.Any())
        {
            _logger.LogWarning(
                "Aucun destinataire trouvé pour {EventType}",
                eventDto.EventType);
            return;
        }
        var resolvedTitle = rule.TaskTitle!;
        if (!string.IsNullOrEmpty(eventDto.ClientName))
            resolvedTitle = resolvedTitle.Replace("{clientName}", eventDto.ClientName);
        if (!string.IsNullOrEmpty(eventDto.ProjectName))
            resolvedTitle = resolvedTitle.Replace("{projectName}", eventDto.ProjectName);

        // create one task per target
        foreach (var userId in targetUserIds)
        {
            await CreateTaskAsync(
                resolvedTitle,
                rule.TaskDescription!,
                userId,
                projectId,
                eventDto.StreamId
                 );
        }
    }

    private async Task<List<Guid>> ResolveTargetsAsync(
    WorkflowRule rule,
    AcpEventDto eventDto)
    {
        var result = new List<Guid>();

        foreach (var targetValue in rule.TargetValues)
        {
            switch (rule.TargetType)
            {
                case "ROLE":
                    if (Enum.TryParse<GlobalRole>(targetValue, out var roleEnum))
                    {
                        var user = await _db.Users
                            .FirstOrDefaultAsync(u => u.Role == roleEnum);
                        if (user != null) result.Add(user.Id);
                    }
                    break;

                case "CONTEXT_USER":
                    var property = eventDto.GetType().GetProperty(targetValue);
                    if (property == null)
                    {
                        _logger.LogWarning(
                            "Propriété introuvable dans AcpEventDto : {Field}",
                            targetValue);
                        break;
                    }
                    var value = property.GetValue(eventDto) as Guid?;
                    if (value.HasValue) result.Add(value.Value);
                    break;
            }
        }

        return result;
    }

    private async Task CreateTaskAsync(
        string title,
        string description,
        Guid userId,
        Guid? projectId,Guid? streamId, Guid? contractId = null)
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
            ProjectId = projectId,
            StreamId = streamId
        };

        _db.AcpTasks.Add(task);
        await _db.SaveChangesAsync();

        await _hubContext.Clients
            .Group(user.KeycloakId)
            .SendAsync("NewNotification", new
            {
                message = title,
                projectId = projectId,
                contractId=contractId
            });

        _logger.LogInformation(
            "Tâche créée : {Title} → {User}",
            title, user.FullName);
    }
}