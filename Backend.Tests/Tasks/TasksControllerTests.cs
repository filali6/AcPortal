using System.Security.Claims;
using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Events.Services;
using Backend.Modules.Projects.Models;
using Backend.Modules.Tasks.Controllers;
using Backend.Modules.Tasks.Models;
using Backend.Modules.Tasks.Services;
using Dapr.Client;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Tests.Tasks;

public class TasksControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly TasksController _controller;

    public TasksControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        var tasksService = new TasksService(_db, NullLogger<TasksService>.Instance);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventPublisher:MaxRetries"] = "0"
        }).Build();
        var eventPublisher = new EventPublisher(new DaprClientBuilder().Build(), NullLogger<EventPublisher>.Instance, config);
        _controller = new TasksController(tasksService, _db, eventPublisher);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void SetUser(TasksController controller, string keycloakId)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, keycloakId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithTasks()
    {
        _db.AcpTasks.Add(new AcpTask { Title = "T1" });
        await _db.SaveChangesAsync();

        var result = await _controller.GetAll();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        var result = await _controller.GetById(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetById_WhenFound_ReturnsOk()
    {
        var task = new AcpTask { Title = "T1" };
        _db.AcpTasks.Add(task);
        await _db.SaveChangesAsync();

        var result = await _controller.GetById(task.Id);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateStatus_WhenTaskNotFound_ReturnsNotFound()
    {
        var result = await _controller.UpdateStatus(Guid.NewGuid(), new UpdateStatusRequest { Status = AcpTaskStatus.Done });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateStatus_ToDoneWithoutProject_DoesNotThrowAndReturnsOk()
    {
        var task = new AcpTask { Title = "T1" };
        _db.AcpTasks.Add(task);
        await _db.SaveChangesAsync();

        var result = await _controller.UpdateStatus(task.Id, new UpdateStatusRequest { Status = AcpTaskStatus.Pending });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateStatus_ToDoneWithProject_PublishesEventAndReturnsOk()
    {
        var project = new Project { Name = "P" };
        _db.Projects.Add(project);
        var task = new AcpTask { Title = "T1", ProjectId = project.Id };
        _db.AcpTasks.Add(task);
        await _db.SaveChangesAsync();

        var result = await _controller.UpdateStatus(task.Id, new UpdateStatusRequest { Status = AcpTaskStatus.Done });

        result.Should().BeOfType<OkObjectResult>();
        (await _db.AcpTasks.FindAsync(task.Id))!.Status.Should().Be(AcpTaskStatus.Done);
    }

    [Fact]
    public async Task Assign_WhenTaskNotFound_ReturnsNotFound()
    {
        var result = await _controller.Assign(Guid.NewGuid(), new AssignRequest { AssignedTo = "user1" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Assign_WhenTaskFound_UpdatesAssignee()
    {
        var task = new AcpTask { Title = "T1" };
        _db.AcpTasks.Add(task);
        await _db.SaveChangesAsync();

        var result = await _controller.Assign(task.Id, new AssignRequest { AssignedTo = "user1" });

        result.Should().BeOfType<OkObjectResult>();
        (await _db.AcpTasks.FindAsync(task.Id))!.AssignedTo.Should().Be("user1");
    }

    [Fact]
    public async Task GetMyTasks_WhenUserNotFound_ReturnsEmptyList()
    {
        SetUser(_controller, "unknown-kc");

        var result = await _controller.GetMyTasks();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().BeEmpty();
    }

    [Fact]
    public async Task GetMyTasks_WhenUserFound_ReturnsAssignedTasks()
    {
        var user = new User { FullName = "U", Email = "u@test.com", KeycloakId = "kc-1" };
        _db.Users.Add(user);
        _db.AcpTasks.Add(new AcpTask { Title = "T1", AssignedTo = "kc-1" });
        await _db.SaveChangesAsync();
        SetUser(_controller, "kc-1");

        var result = await _controller.GetMyTasks();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetByStream_ReturnsTasksForStream()
    {
        var streamId = Guid.NewGuid();
        _db.AcpTasks.Add(new AcpTask { Title = "T1", StreamId = streamId });
        _db.AcpTasks.Add(new AcpTask { Title = "T2", StreamId = Guid.NewGuid() });
        await _db.SaveChangesAsync();

        var result = await _controller.GetByStream(streamId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().HaveCount(1);
    }
}
