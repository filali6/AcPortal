using Backend.Data;
using Backend.Modules.Projects.Models;
using Backend.Modules.Tasks.Models;
using Backend.Modules.Tasks.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Tests.Tasks;

public class TasksServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly TasksService _service;

    public TasksServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _service = new TasksService(_db, NullLogger<TasksService>.Instance);
    }

   public void Dispose()
{
    _db.Dispose();
    GC.SuppressFinalize(this);
}

    [Fact]
    public async Task GetAllAsync_ReturnsTasksOrderedByCreatedAtDescending()
    {
        _db.AcpTasks.AddRange(
            new AcpTask { Title = "Old", CreatedAt = DateTime.UtcNow.AddMinutes(-5) },
            new AcpTask { Title = "New", CreatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var result = await _service.GetAllAsync();

        result.Should().HaveCount(2);
        result[0].GetType().GetProperty("Title")!.GetValue(result[0]).Should().Be("New");
    }

    [Fact]
    public async Task GetAllAsync_FallsBackToStepStreamId_WhenTaskStreamIdIsNull()
    {
        var project = new Project { Name = "P" };
        _db.Projects.Add(project);
        var stream = new Backend.Modules.Projects.Models.Stream { Name = "S", Project = project };
        _db.Streams.Add(stream);
        var step = new ProjectStep { ProjectId = project.Id, StreamId = stream.Id, StepName = "Step", ToolName = "tool" };
        _db.ProjectSteps.Add(step);
        await _db.SaveChangesAsync();
        _db.AcpTasks.Add(new AcpTask { Title = "T", StepId = step.Id, StreamId = null });
        await _db.SaveChangesAsync();

        var result = await _service.GetAllAsync();

        var streamId = result[0].GetType().GetProperty("StreamId")!.GetValue(result[0]);
        streamId.Should().Be(stream.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTask_WhenFound()
    {
        var task = new AcpTask { Title = "T" };
        _db.AcpTasks.Add(task);
        await _db.SaveChangesAsync();

        var result = await _service.GetByIdAsync(task.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("T");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatusAsync_ReturnsNull_WhenTaskNotFound()
    {
        var result = await _service.UpdateStatusAsync(Guid.NewGuid(), AcpTaskStatus.Done);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatusAsync_UpdatesStatusAndTimestamp_WhenTaskFound()
    {
        var task = new AcpTask { Title = "T", Status = AcpTaskStatus.Pending };
        _db.AcpTasks.Add(task);
        await _db.SaveChangesAsync();

        var result = await _service.UpdateStatusAsync(task.Id, AcpTaskStatus.Done);

        result!.Status.Should().Be(AcpTaskStatus.Done);
        result.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AssignAsync_ReturnsNull_WhenTaskNotFound()
    {
        var result = await _service.AssignAsync(Guid.NewGuid(), "kc-1");

        result.Should().BeNull();
    }

    [Fact]
    public async Task AssignAsync_UpdatesAssignedTo_WhenTaskFound()
    {
        var task = new AcpTask { Title = "T" };
        _db.AcpTasks.Add(task);
        await _db.SaveChangesAsync();

        var result = await _service.AssignAsync(task.Id, "kc-2");

        result!.AssignedTo.Should().Be("kc-2");
        result.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetMyTasksAsync_ReturnsOnlyTasksAssignedToConsultant()
    {
        _db.AcpTasks.AddRange(
            new AcpTask { Title = "Mine", AssignedTo = "kc-1" },
            new AcpTask { Title = "Other", AssignedTo = "kc-2" });
        await _db.SaveChangesAsync();

        var result = await _service.GetMyTasksAsync("kc-1");

        result.Should().ContainSingle();
        result[0].GetType().GetProperty("Title")!.GetValue(result[0]).Should().Be("Mine");
    }

    [Fact]
    public async Task GetByStreamAsync_ReturnsTasksDirectlyOnStream_AndViaStep()
    {
        var project = new Project { Name = "P" };
        _db.Projects.Add(project);
        var stream = new Backend.Modules.Projects.Models.Stream { Name = "S", Project = project };
        _db.Streams.Add(stream);
        var otherStream = new Backend.Modules.Projects.Models.Stream { Name = "Other", Project = project };
        _db.Streams.Add(otherStream);
        var step = new ProjectStep { ProjectId = project.Id, StreamId = stream.Id, StepName = "Step", ToolName = "tool" };
        _db.ProjectSteps.Add(step);
        await _db.SaveChangesAsync();

        _db.AcpTasks.AddRange(
            new AcpTask { Title = "Direct", StreamId = stream.Id },
            new AcpTask { Title = "ViaStep", StepId = step.Id },
            new AcpTask { Title = "OtherStream", StreamId = otherStream.Id });
        await _db.SaveChangesAsync();

        var result = await _service.GetByStreamAsync(stream.Id);

        var titles = result.Select(r => r.GetType().GetProperty("Title")!.GetValue(r)).ToList();
        titles.Should().BeEquivalentTo(new[] { "Direct", "ViaStep" });
    }
}
