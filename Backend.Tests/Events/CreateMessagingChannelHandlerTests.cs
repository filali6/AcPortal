using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Events.Handlers;
using Backend.Modules.Events.Models;
using Backend.Hubs;
using Backend.Modules.Messaging.Services;
using Backend.Modules.Notifications.Services;
using Backend.Modules.Projects.Models;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Backend.Tests.Events;

public class CreateMessagingChannelHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IMessagingProvider> _messaging = new();
    private readonly CreateMessagingChannelHandler _handler;

    public CreateMessagingChannelHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(c => c.Group(It.IsAny<string>())).Returns(Mock.Of<IClientProxy>());
        var hub = new Mock<IHubContext<NotificationHub>>();
        hub.Setup(h => h.Clients).Returns(hubClients.Object);

        _handler = new CreateMessagingChannelHandler(
            _db,
            _messaging.Object,
            new EmailService(new ConfigurationBuilder().Build(), NullLogger<EmailService>.Instance),
            new NotificationService(_db, hub.Object),
            NullLogger<CreateMessagingChannelHandler>.Instance);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task HandleAsync_CreatesChannelWithDistinctMembersAndNotifications()
    {
        var duplicate = new User { FullName = "Duplicate", Email = "same@test.com", KeycloakId = "kc-duplicate" };
        var technicalLead = new User { FullName = "Technical", Email = "same@test.com", KeycloakId = "kc-technical" };
        var manager = new User { FullName = "Manager", Email = "manager@test.com", KeycloakId = "kc-manager" };
        var project = new Project { Name = "Project", ProjectManager = manager };
        var stream = new Backend.Modules.Projects.Models.Stream
        {
            Name = "Stream",
            Project = project,
            BusinessTeamLead = duplicate,
            TechnicalTeamLead = technicalLead,
            Members = new List<StreamMember>
            {
                new() { Consultant = duplicate, TeamType = TeamType.Business }
            }
        };
        _db.AddRange(project, stream);
        await _db.SaveChangesAsync();

        _messaging
            .Setup(m => m.CreateStreamChannelAsync("Project", "Stream", It.IsAny<List<string>>()))
            .ReturnsAsync(("C123", "https://slack/channel"));

        await _handler.HandleAsync(
            new WorkflowRule { ActionType = "CREATE_MESSAGING_CHANNEL" },
            new AcpEventDto { StreamId = stream.Id },
            project.Id);

        _messaging.Verify(m => m.CreateStreamChannelAsync(
            "Project", "Stream", It.Is<List<string>>(emails =>
                emails.SequenceEqual(new[] { "same@test.com", "manager@test.com" }))), Times.Once);

        var savedStream = await _db.Streams.FindAsync(stream.Id);
        savedStream!.MessagingChannelId.Should().Be("C123");
        savedStream.MessagingChannelUrl.Should().Be("https://slack/channel");

        var notifications = await _db.Notifications.ToListAsync();
        notifications.Select(n => n.RecipientKeycloakId)
            .Should().BeEquivalentTo(new[] { "kc-duplicate", "kc-technical", "kc-manager" });
    }

    [Fact]
    public async Task HandleAsync_IsIdempotent_WhenChannelAlreadyExists()
    {
        var project = new Project { Name = "Project" };
        var stream = new Backend.Modules.Projects.Models.Stream { Name = "Stream", Project = project, MessagingChannelId = "existing" };
        _db.AddRange(project, stream);
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(
            new WorkflowRule { ActionType = "CREATE_MESSAGING_CHANNEL" },
            new AcpEventDto { StreamId = stream.Id },
            project.Id);

        _messaging.Verify(m => m.CreateStreamChannelAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<string>>()), Times.Never);
    }
}