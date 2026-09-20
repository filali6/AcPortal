using Backend.Data;
using Backend.Hubs;
using Backend.Modules.Notifications.Services;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Backend.Tests.Notifications;

public class NotificationServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IClientProxy> _groupProxy;
    private readonly Mock<IHubClients> _hubClients;
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        _groupProxy = new Mock<IClientProxy>();
        _hubClients = new Mock<IHubClients>();
        _hubClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_groupProxy.Object);
        var hubContext = new Mock<IHubContext<NotificationHub>>();
        hubContext.Setup(h => h.Clients).Returns(_hubClients.Object);

        _service = new NotificationService(_db, hubContext.Object);
    }

   public void Dispose()
{
    _db.Dispose();
    GC.SuppressFinalize(this);
}

    [Fact]
    public async Task SendAsync_PersistsNotification()
    {
        await _service.SendAsync("kc-1", "Hello", "/link");

        var notification = await _db.Notifications.SingleAsync();
        notification.RecipientKeycloakId.Should().Be("kc-1");
        notification.Message.Should().Be("Hello");
        notification.Link.Should().Be("/link");
        notification.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_PushesToHubGroup_ForRecipient()
    {
        await _service.SendAsync("kc-1", "Hello");

        _hubClients.Verify(c => c.Group("kc-1"), Times.Once);
        _groupProxy.Verify(p => p.SendCoreAsync(
            "NewNotification",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByUserAsync_ReturnsOnlyMatchingUser_OrderedByCreatedAtDescending()
    {
        _db.Notifications.AddRange(
            new Backend.Modules.Notifications.Models.Notification { RecipientKeycloakId = "kc-1", Message = "Old", CreatedAt = DateTime.UtcNow.AddMinutes(-10) },
            new Backend.Modules.Notifications.Models.Notification { RecipientKeycloakId = "kc-1", Message = "New", CreatedAt = DateTime.UtcNow },
            new Backend.Modules.Notifications.Models.Notification { RecipientKeycloakId = "kc-2", Message = "Other", CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var result = await _service.GetByUserAsync("kc-1");

        result.Should().HaveCount(2);
        result[0].Message.Should().Be("New");
        result[1].Message.Should().Be("Old");
    }

    [Fact]
    public async Task GetByUserAsync_LimitsResultsTo50()
    {
        for (int i = 0; i < 60; i++)
        {
            _db.Notifications.Add(new Backend.Modules.Notifications.Models.Notification
            {
                RecipientKeycloakId = "kc-1",
                Message = $"Msg {i}",
                CreatedAt = DateTime.UtcNow.AddSeconds(i)
            });
        }
        await _db.SaveChangesAsync();

        var result = await _service.GetByUserAsync("kc-1");

        result.Should().HaveCount(50);
    }

    [Fact]
    public async Task MarkAsReadAsync_SetsIsRead_WhenNotificationExists()
    {
        var notification = new Backend.Modules.Notifications.Models.Notification { RecipientKeycloakId = "kc-1", Message = "Hi" };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();

        await _service.MarkAsReadAsync(notification.Id);

        (await _db.Notifications.FindAsync(notification.Id))!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAsReadAsync_NoOp_WhenNotificationNotFound()
    {
        var act = async () => await _service.MarkAsReadAsync(Guid.NewGuid());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MarkAllAsReadAsync_MarksOnlyUnreadNotifications_ForGivenUser()
    {
        var n1 = new Backend.Modules.Notifications.Models.Notification { RecipientKeycloakId = "kc-1", Message = "A", IsRead = false };
        var n2 = new Backend.Modules.Notifications.Models.Notification { RecipientKeycloakId = "kc-1", Message = "B", IsRead = true };
        var n3 = new Backend.Modules.Notifications.Models.Notification { RecipientKeycloakId = "kc-2", Message = "C", IsRead = false };
        _db.Notifications.AddRange(n1, n2, n3);
        await _db.SaveChangesAsync();

        await _service.MarkAllAsReadAsync("kc-1");

        (await _db.Notifications.FindAsync(n1.Id))!.IsRead.Should().BeTrue();
        (await _db.Notifications.FindAsync(n2.Id))!.IsRead.Should().BeTrue();
        (await _db.Notifications.FindAsync(n3.Id))!.IsRead.Should().BeFalse();
    }
}
