using Backend.Data;
using Backend.Modules.Notifications.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Backend.Hubs;

namespace Backend.Modules.Notifications.Services;

public class NotificationService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hub;

    public NotificationService(AppDbContext db, IHubContext<NotificationHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task SendAsync(string recipientKeycloakId, string message, string? link = null)
    {
        var notification = new Notification
        {
            RecipientKeycloakId = recipientKeycloakId,
            Message = message,
            Link = link
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        await _hub.Clients.Group(recipientKeycloakId)
            .SendAsync("NewNotification", new
            {
                id = notification.Id,
                message = notification.Message,
                link = notification.Link,
                createdAt = notification.CreatedAt,
                isRead = notification.IsRead
            });
    }

    public async Task<List<Notification>> GetByUserAsync(string keycloakId)
    {
        return await _db.Notifications
            .Where(n => n.RecipientKeycloakId == keycloakId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(Guid id)
    {
        var notification = await _db.Notifications.FindAsync(id);
        if (notification == null) return;
        notification.IsRead = true;
        await _db.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(string keycloakId)
    {
        var notifications = await _db.Notifications
            .Where(n => n.RecipientKeycloakId == keycloakId && !n.IsRead)
            .ToListAsync();
        notifications.ForEach(n => n.IsRead = true);
        await _db.SaveChangesAsync();
    }
}