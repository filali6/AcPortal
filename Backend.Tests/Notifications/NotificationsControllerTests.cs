using System.Security.Claims;
using Backend.Data;
using Backend.Hubs;
using Backend.Modules.Notifications.Controllers;
using Backend.Modules.Notifications.Models;
using Backend.Modules.Notifications.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Backend.Tests.Notifications;

public class NotificationsControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly NotificationsController _controller;

    public NotificationsControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(c => c.Group(It.IsAny<string>())).Returns(Mock.Of<IClientProxy>());
        var hub = new Mock<IHubContext<NotificationHub>>();
        hub.Setup(h => h.Clients).Returns(hubClients.Object);
        var notificationService = new NotificationService(_db, hub.Object);

        _controller = new NotificationsController(notificationService, _db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void SetUser(NotificationsController controller, string? keycloakId)
    {
        var claims = keycloakId == null
            ? new List<Claim>()
            : new List<Claim> { new(ClaimTypes.NameIdentifier, keycloakId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [Fact]
    public async Task GetMy_ReturnsUnauthorized_WhenNoClaim()
    {
        SetUser(_controller, null);

        var result = await _controller.GetMy();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetMy_ReturnsOk_WithUsersNotifications()
    {
        _db.Notifications.Add(new Notification { RecipientKeycloakId = "kc-1", Message = "Hi" });
        _db.Notifications.Add(new Notification { RecipientKeycloakId = "kc-2", Message = "Other" });
        await _db.SaveChangesAsync();
        SetUser(_controller, "kc-1");

        var result = await _controller.GetMy();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<Notification>)ok.Value!).Should().ContainSingle();
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsUnauthorized_WhenNoClaim()
    {
        SetUser(_controller, null);

        var result = await _controller.GetUnreadCount();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetUnreadCount_ReturnsOk_WithCorrectCount()
    {
        _db.Notifications.Add(new Notification { RecipientKeycloakId = "kc-1", Message = "Hi", IsRead = false });
        _db.Notifications.Add(new Notification { RecipientKeycloakId = "kc-1", Message = "Read", IsRead = true });
        await _db.SaveChangesAsync();
        SetUser(_controller, "kc-1");

        var result = await _controller.GetUnreadCount();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task MarkAsRead_ReturnsOk_MarksNotificationRead()
    {
        var notification = new Notification { RecipientKeycloakId = "kc-1", Message = "Hi" };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        var result = await _controller.MarkAsRead(notification.Id);

        result.Should().BeOfType<OkResult>();
        (await _db.Notifications.FindAsync(notification.Id))!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAllAsRead_ReturnsUnauthorized_WhenNoClaim()
    {
        SetUser(_controller, null);

        var result = await _controller.MarkAllAsRead();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task MarkAllAsRead_MarksOnlyCurrentUsersNotifications()
    {
        var mine = new Notification { RecipientKeycloakId = "kc-1", Message = "Mine", IsRead = false };
        var other = new Notification { RecipientKeycloakId = "kc-2", Message = "Other", IsRead = false };
        _db.Notifications.AddRange(mine, other);
        await _db.SaveChangesAsync();
        SetUser(_controller, "kc-1");

        var result = await _controller.MarkAllAsRead();

        result.Should().BeOfType<OkResult>();
        (await _db.Notifications.FindAsync(mine.Id))!.IsRead.Should().BeTrue();
        (await _db.Notifications.FindAsync(other.Id))!.IsRead.Should().BeFalse();
    }
}
