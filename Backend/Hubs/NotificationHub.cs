using Backend.Modules.Notifications.Services;
using Microsoft.AspNetCore.SignalR;

namespace Backend.Hubs;

public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        _logger.LogInformation(
            "Client {ConnectionId} joined group {UserId}",
            Context.ConnectionId, userId);
    }
}