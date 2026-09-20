using Backend.Data;
using Backend.Hubs;
using Backend.Modules.Auth.Models;
using Backend.Modules.Events.Handlers;
using Backend.Modules.Events.Models;
using Backend.Modules.Notifications.Services;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Backend.Tests.Events;

public class CreateTaskHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CreateTaskHandler _handler;

    public CreateTaskHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(c => c.Group(It.IsAny<string>())).Returns(Mock.Of<IClientProxy>());
        var hub = new Mock<IHubContext<NotificationHub>>();
        hub.Setup(h => h.Clients).Returns(hubClients.Object);

        _handler = new CreateTaskHandler(_db, new NotificationService(_db, hub.Object), NullLogger<CreateTaskHandler>.Instance);
    }

   public void Dispose()
{
    _db.Dispose();
    GC.SuppressFinalize(this);
}

    [Fact]
    public async Task HandleAsync_ForRoleTarget_CreatesTask_AndReplacesPlaceholders_AndNotifies()
    {
        var head = new User { FullName = "Head", Email = "head@test.com", KeycloakId = "kc-head", Role = GlobalRole.HeadOfCDS };
        _db.Users.Add(head);
        await _db.SaveChangesAsync();

        var rule = new WorkflowRule
        {
            ActionType = "CREATE_TASK",
            TargetType = "ROLE",
            TargetValues = new List<string> { "HeadOfCDS" },
            TaskTitle = "Contract signed for {clientName} ({projectName}/{streamName})",
            TaskDescription = "Review the new contract"
        };
        var eventDto = new AcpEventDto
        {
            ClientName = "Acme",
            ProjectName = "Proj1",
            StreamName = "Stream1"
        };
        var projectId = Guid.NewGuid();

        await _handler.HandleAsync(rule, eventDto, projectId);

        var task = await _db.AcpTasks.SingleAsync();
        task.Title.Should().Be("Contract signed for Acme (Proj1/Stream1)");
        task.Description.Should().Be("Review the new contract");
        task.AssignedTo.Should().Be("kc-head");
        task.ProjectId.Should().Be(projectId);

        var notification = await _db.Notifications.SingleAsync();
        notification.RecipientKeycloakId.Should().Be("kc-head");
    }

    [Fact]
    public async Task HandleAsync_ForContextUserTarget_CreatesTask_ForResolvedUser()
    {
        var director = new User { FullName = "Director", Email = "dir@test.com", KeycloakId = "kc-dir", Role = GlobalRole.PortfolioDirector };
        _db.Users.Add(director);
        await _db.SaveChangesAsync();

        var rule = new WorkflowRule
        {
            ActionType = "CREATE_TASK",
            TargetType = "CONTEXT_USER",
            TargetValues = new List<string> { "DirectorId" },
            TaskTitle = "New project created",
            TaskDescription = "desc"
        };
        var eventDto = new AcpEventDto { DirectorId = director.Id };

        await _handler.HandleAsync(rule, eventDto, null);

        var task = await _db.AcpTasks.SingleAsync();
        task.AssignedTo.Should().Be("kc-dir");
    }

    [Fact]
    public async Task HandleAsync_NoOp_WhenNoTargetsResolved()
    {
        var rule = new WorkflowRule
        {
            ActionType = "CREATE_TASK",
            TargetType = "ROLE",
            TargetValues = new List<string> { "HeadOfCDS" }, // no user with this role exists
            TaskTitle = "T",
            TaskDescription = "D"
        };

        await _handler.HandleAsync(rule, new AcpEventDto(), null);

        (await _db.AcpTasks.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_SkipsCreation_WhenResolvedUserIdDoesNotExistInDb()
    {
        var rule = new WorkflowRule
        {
            ActionType = "CREATE_TASK",
            TargetType = "CONTEXT_USER",
            TargetValues = new List<string> { "DirectorId" },
            TaskTitle = "T",
            TaskDescription = "D"
        };
        var eventDto = new AcpEventDto { DirectorId = Guid.NewGuid() }; // not seeded in db

        await _handler.HandleAsync(rule, eventDto, null);

        (await _db.AcpTasks.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_CreatesOneTaskPerTargetValue_ForMultipleRoles()
    {
        _db.Users.Add(new User { FullName = "Head", Email = "head@test.com", KeycloakId = "kc-head", Role = GlobalRole.HeadOfCDS });
        _db.Users.Add(new User { FullName = "Cons", Email = "cons@test.com", KeycloakId = "kc-cons", Role = GlobalRole.Consultant });
        await _db.SaveChangesAsync();

        var rule = new WorkflowRule
        {
            ActionType = "CREATE_TASK",
            TargetType = "ROLE",
            TargetValues = new List<string> { "HeadOfCDS", "Consultant" },
            TaskTitle = "T",
            TaskDescription = "D"
        };

        await _handler.HandleAsync(rule, new AcpEventDto(), null);

        var tasks = await _db.AcpTasks.ToListAsync();
        tasks.Should().HaveCount(2);
        tasks.Select(t => t.AssignedTo).Should().BeEquivalentTo(new[] { "kc-head", "kc-cons" });
    }
}
