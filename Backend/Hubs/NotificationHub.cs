using Microsoft.AspNetCore.SignalR;
namespace Backend.Hubs;

public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub>_logger;
    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger=logger;
    }
    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId,userId);
        _logger.LogInformation(
            "Client {ConnectionId} a rejoint le groupe {UserId}",
            Context.ConnectionId, userId);

    }
}