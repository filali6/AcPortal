using System.Security.Claims;
using Backend.Data;
using Backend.Hubs;
using Backend.Modules.Auth.Models;
using Backend.Modules.Events.Services;
using Backend.Modules.Messaging.Services;
using Backend.Modules.Notifications.Services;
using Backend.Modules.Tasks.Controllers;
using Backend.Modules.Tasks.Models;
using Backend.Modules.Tasks.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Backend.Tests.Tasks;

public class TaskCommentsControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly TaskCommentsController _controller;

    public TaskCommentsControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(c => c.Group(It.IsAny<string>())).Returns(Mock.Of<IClientProxy>());
        var hubContext = new Mock<IHubContext<NotificationHub>>();
        hubContext.Setup(h => h.Clients).Returns(hubClients.Object);

        var notificationService = new NotificationService(_db, hubContext.Object);
        var teamsNotificationService = new TeamsNotificationService(new HttpClient(), new ConfigurationBuilder().Build());
        var messagingProvider = new Mock<IMessagingProvider>();
        var messagingCommentSync = new MessagingCommentSyncService(
            _db, messagingProvider.Object, NullLogger<MessagingCommentSyncService>.Instance);
        var emailService = new EmailService(new ConfigurationBuilder().Build(), NullLogger<EmailService>.Instance);
        var eventPublisher = new EventPublisher(
            new Dapr.Client.DaprClientBuilder().Build(),
            NullLogger<EventPublisher>.Instance,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["EventPublisher:MaxRetries"] = "0" })
                .Build());

        var service = new TaskCommentsService(
            _db, notificationService, teamsNotificationService, messagingCommentSync, emailService, eventPublisher);
        _controller = new TaskCommentsController(service, _db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void SetUser(TaskCommentsController controller, string keycloakId)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, keycloakId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [Fact]
    public async Task GetComments_ReturnsOk()
    {
        var task = new AcpTask { Title = "T1" };
        _db.AcpTasks.Add(task);
        await _db.SaveChangesAsync();

        var result = await _controller.GetComments(task.Id);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AddComment_WhenUnauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await _controller.AddComment(Guid.NewGuid(), new CreateCommentDto { Content = "hi" });

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task AddComment_WhenUserNotFound_ReturnsNotFound()
    {
        SetUser(_controller, "unknown-kc");

        var result = await _controller.AddComment(Guid.NewGuid(), new CreateCommentDto { Content = "hi" });

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task AddComment_WithValidData_PersistsComment()
    {
        var user = new User { FullName = "Author", Email = "a@test.com", KeycloakId = "kc-1" };
        var task = new AcpTask { Title = "T1" };
        _db.Users.Add(user);
        _db.AcpTasks.Add(task);
        await _db.SaveChangesAsync();
        SetUser(_controller, "kc-1");

        var result = await _controller.AddComment(task.Id, new CreateCommentDto { Content = "hello" });

        result.Should().BeOfType<OkObjectResult>();
        (await _db.TaskComments.SingleAsync()).Content.Should().Be("hello");
    }

    [Fact]
    public async Task DeleteComment_WhenUnauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await _controller.DeleteComment(Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task DeleteComment_WhenNotAuthorOrNotFound_ReturnsForbid()
    {
        SetUser(_controller, "kc-1");

        var result = await _controller.DeleteComment(Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task DeleteComment_WhenAuthor_RemovesComment()
    {
        var task = new AcpTask { Title = "T1" };
        var comment = new TaskComment { TaskId = task.Id, AuthorKeycloakId = "kc-1", Content = "hi" };
        _db.AcpTasks.Add(task);
        _db.TaskComments.Add(comment);
        await _db.SaveChangesAsync();
        SetUser(_controller, "kc-1");

        var result = await _controller.DeleteComment(task.Id, comment.Id);

        result.Should().BeOfType<OkResult>();
        (await _db.TaskComments.FindAsync(comment.Id)).Should().BeNull();
    }
}
