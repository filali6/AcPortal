using Backend.Data;
using Backend.Modules.Sla.Services;

using Backend.Modules.Projects.Models;
using Backend.Modules.Sla.Models;
using Backend.Modules.Tasks.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests.Sla;

public class SlaCheckerServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SlaCheckerService _service;

    public SlaCheckerServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _service = new SlaCheckerService(_db, NullLogger<SlaCheckerService>.Instance);
    }

   public void Dispose()
{
    _db.Dispose();
    GC.SuppressFinalize(this);
}

    private static AcpTask CreateTask(DateTime? dueDate, AcpTaskStatus status = AcpTaskStatus.Pending) =>
        new()
        {
            Title = "Task",
            DueDate = dueDate,
            Status = status
        };

    // ── GetTaskSlaStatus ───────────────────────────────────────────

    [Fact]
    public void GetTaskSlaStatus_ShouldReturnOnTrack_WhenDueDateIsNull()
    {
        var task = CreateTask(null);

        _service.GetTaskSlaStatus(task).Should().Be(SlaStatus.OnTrack);
    }

    [Fact]
    public void GetTaskSlaStatus_ShouldReturnOverdue_WhenDueDateIsInThePast()
    {
        var task = CreateTask(DateTime.UtcNow.AddDays(-1));

        _service.GetTaskSlaStatus(task).Should().Be(SlaStatus.Overdue);
    }

    [Fact]
    public void GetTaskSlaStatus_ShouldReturnAtRisk_WhenDueWithinTwoDays()
    {
        var task = CreateTask(DateTime.UtcNow.AddDays(1));

        _service.GetTaskSlaStatus(task).Should().Be(SlaStatus.AtRisk);
    }

    [Fact]
    public void GetTaskSlaStatus_ShouldReturnOnTrack_WhenDueDateIsFarInTheFuture()
    {
        var task = CreateTask(DateTime.UtcNow.AddDays(10));

        _service.GetTaskSlaStatus(task).Should().Be(SlaStatus.OnTrack);
    }

    // ── GetStreamSlaStatus ─────────────────────────────────────────

    [Fact]
    public void GetStreamSlaStatus_ShouldReturnOnTrack_WhenDueDateIsNull()
    {
        _service.GetStreamSlaStatus(null).Should().Be(SlaStatus.OnTrack);
    }

    [Fact]
    public void GetStreamSlaStatus_ShouldReturnOverdue_WhenDueDateIsInThePast()
    {
        _service.GetStreamSlaStatus(DateTime.UtcNow.AddDays(-1)).Should().Be(SlaStatus.Overdue);
    }

    [Fact]
    public void GetStreamSlaStatus_ShouldReturnAtRisk_WhenDueWithinThreeDays()
    {
        _service.GetStreamSlaStatus(DateTime.UtcNow.AddDays(2)).Should().Be(SlaStatus.AtRisk);
    }

    [Fact]
    public void GetStreamSlaStatus_ShouldReturnOnTrack_WhenDueDateIsFarInTheFuture()
    {
        _service.GetStreamSlaStatus(DateTime.UtcNow.AddDays(10)).Should().Be(SlaStatus.OnTrack);
    }

    // ── ApplySlaRulesToTasksAsync ──────────────────────────────────

    [Fact]
    public async Task ApplySlaRulesToTasksAsync_ShouldDoNothing_WhenNoTaskRulesExist()
    {
        var task = CreateTask(null);
        _db.AcpTasks.Add(task);
        await _db.SaveChangesAsync();

        await _service.ApplySlaRulesToTasksAsync();

        var reloaded = await _db.AcpTasks.FindAsync(task.Id);
        reloaded!.DueDate.Should().BeNull();
        reloaded.SlaRuleId.Should().BeNull();
    }

    [Fact]
    public async Task ApplySlaRulesToTasksAsync_ShouldApplyShortestTaskRule_ToTasksWithoutDueDate()
    {
        var shortRule = new SlaRule { Name = "Short", Type = SlaRuleType.Task, SlaDays = 3 };
        var longRule = new SlaRule { Name = "Long", Type = SlaRuleType.Task, SlaDays = 10 };
        var streamRule = new SlaRule { Name = "StreamOnly", Type = SlaRuleType.Stream, SlaDays = 1 };
        _db.SlaRules.AddRange(shortRule, longRule, streamRule);

        var createdAt = DateTime.UtcNow;
        var task = CreateTask(null);
        task.CreatedAt = createdAt;
        _db.AcpTasks.Add(task);
        await _db.SaveChangesAsync();

        await _service.ApplySlaRulesToTasksAsync();

        var reloaded = await _db.AcpTasks.FindAsync(task.Id);
        reloaded!.SlaRuleId.Should().Be(shortRule.Id);
        reloaded.DueDate.Should().BeCloseTo(createdAt.AddDays(shortRule.SlaDays), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ApplySlaRulesToTasksAsync_ShouldNotModify_TasksThatAlreadyHaveDueDateOrAreDone()
    {
        _db.SlaRules.Add(new SlaRule { Name = "Rule", Type = SlaRuleType.Task, SlaDays = 5 });

        var alreadyDue = CreateTask(DateTime.UtcNow.AddDays(5));
        var doneTask = CreateTask(null, AcpTaskStatus.Done);
        _db.AcpTasks.AddRange(alreadyDue, doneTask);
        await _db.SaveChangesAsync();

        await _service.ApplySlaRulesToTasksAsync();

        (await _db.AcpTasks.FindAsync(doneTask.Id))!.DueDate.Should().BeNull();
        (await _db.AcpTasks.FindAsync(alreadyDue.Id))!.SlaRuleId.Should().BeNull();
    }

    // ── GetOverdueAndAtRiskTasksAsync ──────────────────────────────

    [Fact]
    public async Task GetOverdueAndAtRiskTasksAsync_ShouldReturnOnlyOverdueOrAtRiskOpenTasks()
    {
        var overdue = CreateTask(DateTime.UtcNow.AddDays(-2));
        var atRisk = CreateTask(DateTime.UtcNow.AddHours(12));
        var onTrack = CreateTask(DateTime.UtcNow.AddDays(20));
        var doneButOverdue = CreateTask(DateTime.UtcNow.AddDays(-2), AcpTaskStatus.Done);
        var noDueDate = CreateTask(null);
        _db.AcpTasks.AddRange(overdue, atRisk, onTrack, doneButOverdue, noDueDate);
        await _db.SaveChangesAsync();

        var result = await _service.GetOverdueAndAtRiskTasksAsync();

        result.Should().HaveCount(2);
        var ids = result.Select(r => (Guid)r.GetType().GetProperty("Id")!.GetValue(r)!).ToList();
        ids.Should().Contain(new[] { overdue.Id, atRisk.Id });
        ids.Should().NotContain(new[] { onTrack.Id, doneButOverdue.Id, noDueDate.Id });
    }

    [Fact]
    public async Task GetOverdueAndAtRiskTasksAsync_ShouldOrderByDaysRemainingAscending()
    {
        var moreOverdue = CreateTask(DateTime.UtcNow.AddDays(-5));
        var lessOverdue = CreateTask(DateTime.UtcNow.AddDays(-1));
        _db.AcpTasks.AddRange(lessOverdue, moreOverdue);
        await _db.SaveChangesAsync();

        var result = await _service.GetOverdueAndAtRiskTasksAsync();

        var ids = result.Select(r => (Guid)r.GetType().GetProperty("Id")!.GetValue(r)!).ToList();
        ids.Should().Equal(moreOverdue.Id, lessOverdue.Id);
    }

    // ── GetOverdueAndAtRiskStreamsAsync ─────────────────────────────

    [Fact]
    public async Task GetOverdueAndAtRiskStreamsAsync_ShouldReturnOnlyOverdueOrAtRiskStreams()
    {
        var project = new Project { Name = "Project A" };
        _db.Projects.Add(project);

        var overdueStream = new Backend.Modules.Projects.Models.Stream
        {
            Name = "Stream Overdue",
            ProjectId = project.Id,
            Project = project,
            DueDate = DateTime.UtcNow.AddDays(-1)
        };
        var onTrackStream = new Backend.Modules.Projects.Models.Stream
        {
            Name = "Stream OnTrack",
            ProjectId = project.Id,
            Project = project,
            DueDate = DateTime.UtcNow.AddDays(30)
        };
        var noDueDateStream = new Backend.Modules.Projects.Models.Stream
        {
            Name = "Stream NoDueDate",
            ProjectId = project.Id,
            Project = project,
            DueDate = null
        };
        _db.Streams.AddRange(overdueStream, onTrackStream, noDueDateStream);
        await _db.SaveChangesAsync();

        var result = await _service.GetOverdueAndAtRiskStreamsAsync();

        result.Should().ContainSingle();
        var id = (Guid)result[0].GetType().GetProperty("Id")!.GetValue(result[0])!;
        id.Should().Be(overdueStream.Id);
    }
}
