using Backend.Modules.Notifications.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Backend.Data;

namespace Backend.Modules.Notifications.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NotificationService _notificationService;
    private readonly AppDbContext _db;

    public NotificationsController(NotificationService notificationService, AppDbContext db)
    {
        _notificationService = notificationService;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetMy()
    {
        var keycloakId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();

        var notifications = await _notificationService.GetByUserAsync(keycloakId);
        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var keycloakId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();

        var count = await _db.Notifications
            .CountAsync(n => n.RecipientKeycloakId == keycloakId && !n.IsRead);
        return Ok(new { count });
    }

    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        await _notificationService.MarkAsReadAsync(id);
        return Ok();
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var keycloakId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();
        await _notificationService.MarkAllAsReadAsync(keycloakId);
        return Ok();
    }
}