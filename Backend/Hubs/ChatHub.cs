using Backend.Modules.Chat.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace Backend.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(ChatService chatService, ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _logger = logger;
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
            content, keycloakId, name,
            Guid.Parse(streamId), null);

        await Clients.Group($"stream-{streamId}").SendAsync("ReceiveMessage", new
        {
            id = message.Id,
            content = message.Content,
            senderName = message.SenderName,
            senderKeycloakId = message.SenderKeycloakId,
            createdAt = message.CreatedAt
        });
    }

    public async Task SendTaskMessage(string taskId, string content)
    {
        var keycloakId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        var name = Context.User?.FindFirst("name")?.Value
            ?? Context.User?.FindFirst("preferred_username")?.Value
            ?? "Unknown";

        var message = await _chatService.SaveMessageAsync(
            content, keycloakId, name,
            null, Guid.Parse(taskId));

        await Clients.Group($"task-{taskId}").SendAsync("ReceiveMessage", new
        {
            id = message.Id,
            content = message.Content,
            senderName = message.SenderName,
            senderKeycloakId = message.SenderKeycloakId,
            createdAt = message.CreatedAt
        });
    }
}