using System.Security.Claims;
using Backend.Data;
using Backend.Modules.Projects.Models;
using Backend.Modules.Sla.Controllers;
using Backend.Modules.Sla.Models;
using Backend.Modules.Sla.Services;
using Backend.Modules.Tasks.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ProjectStream = Backend.Modules.Projects.Models.Stream;

namespace Backend.Tests.Sla;

public class SlaControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SlaController _controller;

    public SlaControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        var slaChecker = new SlaCheckerService(_db, NullLogger<SlaCheckerService>.Instance);
        _controller = new SlaController(_db, slaChecker);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void SetUser(SlaController controller, string? keycloakId)
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
    public async Task GetDashboard_ReturnsOk_WithOverdueTasksAndStreams()
    {
        _db.AcpTasks.Add(new AcpTask { Title = "Overdue", DueDate = DateTime.UtcNow.AddDays(-1) });
        var project = new Project { Name = "P1" };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        _db.Streams.Add(new ProjectStream { Name = "S1", ProjectId = project.Id, DueDate = DateTime.UtcNow.AddDays(-1) });
        await _db.SaveChangesAsync();

        var result = await _controller.GetDashboard();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMyTasksWithSla_ReturnsUnauthorized_WhenNoClaim()
    {
        SetUser(_controller, null);

        var result = await _controller.GetMyTasksWithSla();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetMyTasksWithSla_ReturnsOk_WithTasksAssignedToUser()
    {
        _db.AcpTasks.Add(new AcpTask { Title = "T1", AssignedTo = "kc-1", Status = AcpTaskStatus.Pending, DueDate = DateTime.UtcNow.AddDays(1) });
        _db.AcpTasks.Add(new AcpTask { Title = "T2", AssignedTo = "kc-2", Status = AcpTaskStatus.Pending });
        await _db.SaveChangesAsync();
        SetUser(_controller, "kc-1");

        var result = await _controller.GetMyTasksWithSla();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().ContainSingle();
    }

    [Fact]
    public async Task SetStreamDueDate_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.SetStreamDueDate(Guid.NewGuid(), new SetDueDateRequest { DueDate = DateTime.UtcNow });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task SetStreamDueDate_ReturnsOk_SetsDueDate()
    {
        var project = new Project { Name = "P1" };
        _db.Projects.Add(project);
        var stream = new ProjectStream { Name = "S1", ProjectId = project.Id };
        _db.Streams.Add(stream);
        await _db.SaveChangesAsync();
        var dueDate = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Unspecified);

        var result = await _controller.SetStreamDueDate(stream.Id, new SetDueDateRequest { DueDate = dueDate });

        result.Should().BeOfType<OkObjectResult>();
        (await _db.Streams.FindAsync(stream.Id))!.DueDate!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task ApplyRules_ReturnsOk_AppliesDefaultRuleToTasksWithoutDueDate()
    {
        _db.SlaRules.Add(new SlaRule { Name = "Default", Type = SlaRuleType.Task, SlaDays = 5 });
        _db.AcpTasks.Add(new AcpTask { Title = "NoDueDate", Status = AcpTaskStatus.Pending });
        await _db.SaveChangesAsync();

        var result = await _controller.ApplyRules();

        result.Should().BeOfType<OkObjectResult>();
        var task = await _db.AcpTasks.FirstAsync();
        task.DueDate.Should().NotBeNull();
    }
}
