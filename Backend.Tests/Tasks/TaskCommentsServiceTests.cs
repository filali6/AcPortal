using System.Text.Json;
using Backend.Data;
using Backend.Hubs;
using Backend.Modules.Auth.Models;
using Backend.Modules.Notifications.Services;
using Backend.Modules.Tasks.Models;
using Backend.Modules.Tasks.Services;
using Backend.Modules.Teams.Services;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ProjectStream = Backend.Modules.Projects.Models.Stream;
using StreamMember = Backend.Modules.Projects.Models.StreamMember;
using TeamType = Backend.Modules.Projects.Models.TeamType;

namespace Backend.Tests.Tasks;

public class TaskCommentsServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly TaskCommentsService _service;

    public TaskCommentsServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        // IHubContext is the only true mock needed; the rest are real services
        // wired with inert config so their side effects (HTTP/SMTP/Graph calls) no-op.
        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(c => c.Group(It.IsAny<string>())).Returns(Mock.Of<IClientProxy>());
        var hubContext = new Mock<IHubContext<NotificationHub>>();
        hubContext.Setup(h => h.Clients).Returns(hubClients.Object);

        var notificationService = new NotificationService(_db, hubContext.Object);
        var teamsNotificationService = new TeamsNotificationService(new HttpClient(), new ConfigurationBuilder().Build());

        var graphConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MicrosoftGraph:ClientId"] = "11111111-1111-1111-1111-111111111111",
                ["MicrosoftGraph:TenantId"] = "11111111-1111-1111-1111-111111111111",
                ["MicrosoftGraph:ClientSecret"] = "dummy-secret"
            })
            .Build();
        var graphService = new GraphService(graphConfig, NullLogger<GraphService>.Instance);
        var teamsCommentSync = new TeamsCommentSyncService(_db, graphService, NullLogger<TeamsCommentSyncService>.Instance);

        var emailService = new EmailService(new ConfigurationBuilder().Build(), NullLogger<EmailService>.Instance);

        _service = new TaskCommentsService(_db, notificationService, teamsNotificationService, teamsCommentSync, emailService);
    }

    public void Dispose() => _db.Dispose();

    private (ProjectStream stream, AcpTask task) SeedTaskWithStream(
        string? businessLeadKeycloakId = null,
        string? memberKeycloakId = null)
    {
        var project = new Backend.Modules.Projects.Models.Project { Name = "Project" };
        _db.Projects.Add(project);

        User? lead = null;
        if (businessLeadKeycloakId != null)
        {
            lead = new User { FullName = "Lead", Email = $"{businessLeadKeycloakId}@test.com", KeycloakId = businessLeadKeycloakId };
            _db.Users.Add(lead);
        }

        User? member = null;
        if (memberKeycloakId != null)
        {
            member = new User { FullName = "Member", Email = $"{memberKeycloakId}@test.com", KeycloakId = memberKeycloakId };
            _db.Users.Add(member);
        }

        var stream = new ProjectStream
        {
            Name = "Stream1",
            ProjectId = project.Id,
            BusinessTeamLeadId = lead?.Id
        };
        _db.Streams.Add(stream);

        if (member != null)
        {
            _db.StreamMembers.Add(new StreamMember
            {
                StreamId = stream.Id,
                ConsultantId = member.Id,
                TeamType = TeamType.Business
            });
        }

        var task = new AcpTask { Title = "Task1", StreamId = stream.Id };
        _db.AcpTasks.Add(task);

        _db.SaveChanges();

        return (stream, task);
    }

    [Fact]
    public async Task GetCommentsAsync_ReturnsOnlyTopLevelComments_WithNestedReplies()
    {
        var (_, task) = SeedTaskWithStream();

        var parent = new TaskComment { TaskId = task.Id, Content = "Parent", AuthorName = "A", AuthorKeycloakId = "kc-a" };
        _db.TaskComments.Add(parent);
        await _db.SaveChangesAsync();

        _db.TaskComments.Add(new TaskComment { TaskId = task.Id, Content = "Reply", AuthorName = "B", AuthorKeycloakId = "kc-b", ParentCommentId = parent.Id });
        _db.TaskComments.Add(new TaskComment { TaskId = task.Id, Content = "Second top-level", AuthorName = "C", AuthorKeycloakId = "kc-c" });
        await _db.SaveChangesAsync();

        var result = await _service.GetCommentsAsync(task.Id);

        result.Should().HaveCount(2);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var parentJson = doc.RootElement.EnumerateArray().First(e => e.GetProperty("Content").GetString() == "Parent");
        var replies = parentJson.GetProperty("replies");
        replies.GetArrayLength().Should().Be(1);
        replies[0].GetProperty("Content").GetString().Should().Be("Reply");
    }

    [Fact]
    public async Task AddCommentAsync_PersistsComment_AndNotifiesStreamMembers()
    {
        var (_, task) = SeedTaskWithStream(businessLeadKeycloakId: "kc-lead", memberKeycloakId: "kc-member");

        var comment = await _service.AddCommentAsync(task.Id, "Hello team", "kc-author", "Author", null, null);

        comment.Id.Should().NotBeEmpty();
        (await _db.TaskComments.CountAsync()).Should().Be(1);

        var recipients = await _db.Notifications.Select(n => n.RecipientKeycloakId).ToListAsync();
        recipients.Should().BeEquivalentTo(new[] { "kc-lead", "kc-member" });
    }

    [Fact]
    public async Task AddCommentAsync_DoesNotNotifyAuthor_EvenIfAuthorIsStreamMember()
    {
        var (_, task) = SeedTaskWithStream(memberKeycloakId: "kc-author");

        await _service.AddCommentAsync(task.Id, "Hi", "kc-author", "Author", null, null);

        (await _db.Notifications.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task AddCommentAsync_SendsMentionNotification_ForNonStreamMember()
    {
        var (_, task) = SeedTaskWithStream();

        await _service.AddCommentAsync(task.Id, "Hi @someone", "kc-author", "Author", null, new List<string> { "kc-mentioned" });

        var notifications = await _db.Notifications.ToListAsync();
        notifications.Should().ContainSingle(n => n.RecipientKeycloakId == "kc-mentioned" && n.Message.Contains("mentioned"));
    }

    [Fact]
    public async Task DeleteCommentAsync_RemovesComment_WhenAuthorMatches()
    {
        var (_, task) = SeedTaskWithStream();
        var comment = new TaskComment { TaskId = task.Id, Content = "x", AuthorName = "A", AuthorKeycloakId = "kc-a" };
        _db.TaskComments.Add(comment);
        await _db.SaveChangesAsync();

        var result = await _service.DeleteCommentAsync(comment.Id, task.Id, "kc-a");

        result.Should().BeTrue();
        (await _db.TaskComments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteCommentAsync_ReturnsFalse_WhenAuthorDoesNotMatch()
    {
        var (_, task) = SeedTaskWithStream();
        var comment = new TaskComment { TaskId = task.Id, Content = "x", AuthorName = "A", AuthorKeycloakId = "kc-a" };
        _db.TaskComments.Add(comment);
        await _db.SaveChangesAsync();

        var result = await _service.DeleteCommentAsync(comment.Id, task.Id, "kc-other");

        result.Should().BeFalse();
        (await _db.TaskComments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DeleteCommentAsync_ReturnsFalse_WhenCommentDoesNotExist()
    {
        var (_, task) = SeedTaskWithStream();

        var result = await _service.DeleteCommentAsync(Guid.NewGuid(), task.Id, "kc-a");

        result.Should().BeFalse();
    }
}
