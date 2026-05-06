using Backend.Data;
using Backend.Modules.Tasks.Models;
using Backend.Modules.Chat.Services;
using Backend.Modules.Notifications.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Backend.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatHub> _logger;
    private readonly NotificationService _notificationService;
    private readonly AppDbContext _db;
    public ChatHub(ChatService chatService, ILogger<ChatHub> logger,NotificationService notificationService,AppDbContext db)
    {
        _chatService = chatService;
        _logger = logger;
        _notificationService=notificationService;
        _db=db;
    }

    public async Task JoinStreamChat(string streamId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"stream-{streamId}");
        _logger.LogInformation("User joined stream chat: {StreamId}", streamId);
    }

    public async Task JoinTaskChat(string taskId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"task-{taskId}");
        _logger.LogInformation("User joined task chat: {TaskId}", taskId);
    }

    public async Task SendStreamMessage(string streamId, string content)
    {
        var keycloakId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        var name = Context.User?.FindFirst("name")?.Value
            ?? Context.User?.FindFirst("preferred_username")?.Value
            ?? "Unknown";

        var message = await _chatService.SaveMessageAsync(
            content, keycloakId, name, Guid.Parse(streamId), null);

        await Clients.Group($"stream-{streamId}").SendAsync("ReceiveMessage", new
        {
            id = message.Id,
            content = message.Content,
            senderName = message.SenderName,
            senderKeycloakId = message.SenderKeycloakId,
            createdAt = message.CreatedAt
        });

        // Notifier les membres du stream
        var stream = await _db.Streams
            .Include(s => s.Members).ThenInclude(m => m.Consultant)
            .Include(s => s.BusinessTeamLead)
            .Include(s => s.TechnicalTeamLead)
            .FirstOrDefaultAsync(s => s.Id == Guid.Parse(streamId));

        if (stream == null) return;

        var notifMessage = $"{name}: {content}";

        // Membres consultants
        foreach (var member in stream.Members.Where(m => m.Consultant.KeycloakId != keycloakId))
        {
            await _notificationService.SendAsync(member.Consultant.KeycloakId, notifMessage, null);
        }

        // BTL
        if (stream.BusinessTeamLead?.KeycloakId != null && stream.BusinessTeamLead.KeycloakId != keycloakId)
            await _notificationService.SendAsync(stream.BusinessTeamLead.KeycloakId, notifMessage, null);

        // TTL
        if (stream.TechnicalTeamLead?.KeycloakId != null && stream.TechnicalTeamLead.KeycloakId != keycloakId)
            await _notificationService.SendAsync(stream.TechnicalTeamLead.KeycloakId, notifMessage, null);
    }



    public async Task SendTaskMessage(string taskId, string content)
    {
        var keycloakId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        var name = Context.User?.FindFirst("name")?.Value
            ?? Context.User?.FindFirst("preferred_username")?.Value
            ?? "Unknown";

        var message = await _chatService.SaveMessageAsync(
            content, keycloakId, name, null, Guid.Parse(taskId));

        await Clients.Group($"task-{taskId}").SendAsync("ReceiveMessage", new
        {
            id = message.Id,
            content = message.Content,
            senderName = message.SenderName,
            senderKeycloakId = message.SenderKeycloakId,
            createdAt = message.CreatedAt
        });

        var task = await _db.AcpTasks
            .Include(t => t.StreamId)
            .FirstOrDefaultAsync(t => t.Id == Guid.Parse(taskId));

        if (task == null) return;

        var notifMessage = $"{name}: {content}";

        // Notifier le consultant assigné
        if (task.AssignedTo != keycloakId)
            await _notificationService.SendAsync(task.AssignedTo, notifMessage, null);

        // Notifier les leads du stream
        if (task.StreamId.HasValue)
        {
            var stream = await _db.Streams
                .Include(s => s.BusinessTeamLead)
                .Include(s => s.TechnicalTeamLead)
                .FirstOrDefaultAsync(s => s.Id == task.StreamId);

            if (stream != null)
            {
                if (stream.BusinessTeamLead?.KeycloakId != null &&
                    stream.BusinessTeamLead.KeycloakId != keycloakId)
                    await _notificationService.SendAsync(
                        stream.BusinessTeamLead.KeycloakId, notifMessage, null);

                if (stream.TechnicalTeamLead?.KeycloakId != null &&
                    stream.TechnicalTeamLead.KeycloakId != keycloakId)
                    await _notificationService.SendAsync(
                        stream.TechnicalTeamLead.KeycloakId, notifMessage, null);
            }
        }
    }
}