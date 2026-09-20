using Backend.Data;
using Backend.Modules.Messaging.Services;
using Backend.Modules.Tasks.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Backend.Tests.Messaging;

public class MessagingCommentSyncServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IMessagingProvider> _messaging = new();
    private readonly MessagingCommentSyncService _service;

    public MessagingCommentSyncServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _service = new MessagingCommentSyncService(
            _db, _messaging.Object, NullLogger<MessagingCommentSyncService>.Instance);
    }

   public void Dispose()
{
    _db.Dispose();
    GC.SuppressFinalize(this);
}

    [Fact]
    public async Task SyncCommentToMessagingAsync_CreatesThreadAndPersistsIds_WhenTaskHasChannel()
    {
        var project = new Backend.Modules.Projects.Models.Project { Name = "Project" };
        var stream = new Backend.Modules.Projects.Models.Stream
        {
            Name = "Stream",
            Project = project,
            MessagingChannelId = "C123"
        };
        var task = new AcpTask { Title = "Fix bug", StreamId = stream.Id };
        _db.AddRange(project, stream, task);
        await _db.SaveChangesAsync();

        _messaging
            .Setup(m => m.PostThreadMessageAsync("C123", "💬 *Alice* on [Fix bug]: Done"))
            .ReturnsAsync(("171.42", "https://slack/thread"));

        await _service.SyncCommentToMessagingAsync(task.Id, "Alice", "Done");

        var savedTask = await _db.AcpTasks.FindAsync(task.Id);
        savedTask!.MessagingThreadId.Should().Be("171.42");
        savedTask.MessagingThreadUrl.Should().Be("https://slack/thread");
        _messaging.Verify(m => m.PostThreadMessageAsync("C123", "💬 *Alice* on [Fix bug]: Done"), Times.Once);
        _messaging.Verify(m => m.ReplyToThreadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SyncCommentToMessagingAsync_RepliesToExistingThread()
    {
        var project = new Backend.Modules.Projects.Models.Project { Name = "Project" };
        var stream = new Backend.Modules.Projects.Models.Stream
        {
            Name = "Stream",
            Project = project,
            MessagingChannelId = "C123"
        };
        var task = new AcpTask
        {
            Title = "Fix bug",
            StreamId = stream.Id,
            MessagingThreadId = "171.42"
        };
        _db.AddRange(project, stream, task);
        await _db.SaveChangesAsync();

        await _service.SyncCommentToMessagingAsync(task.Id, "Bob", "Still investigating");

        _messaging.Verify(m => m.ReplyToThreadAsync(
            "C123", "171.42", "💬 *Bob* on [Fix bug]: Still investigating"), Times.Once);
        _messaging.Verify(m => m.PostThreadMessageAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SyncCommentToMessagingAsync_UsesStepStream_WhenTaskHasNoStream()
    {
        var project = new Backend.Modules.Projects.Models.Project { Name = "Project" };
        var stream = new Backend.Modules.Projects.Models.Stream
        {
            Name = "Stream",
            Project = project,
            MessagingChannelId = "C123"
        };
        var step = new Backend.Modules.Projects.Models.ProjectStep
        {
            Project = project,
            Stream = stream
        };
        var task = new AcpTask { Title = "Step task", StepId = step.Id };
        _db.AddRange(step, task);
        await _db.SaveChangesAsync();

        _messaging
            .Setup(m => m.PostThreadMessageAsync("C123", It.IsAny<string>()))
            .ReturnsAsync(("171.43", (string?)null));

        await _service.SyncCommentToMessagingAsync(task.Id, "Alice", "Ready");

        _messaging.Verify(m => m.PostThreadMessageAsync(
            "C123", "💬 *Alice* on [Step task]: Ready"), Times.Once);
    }

    [Fact]
    public async Task SyncCommentToMessagingAsync_DoesNothing_WhenTaskOrChannelIsUnavailable()
    {
        await _service.SyncCommentToMessagingAsync(Guid.NewGuid(), "Alice", "Ignored");

        var project = new Backend.Modules.Projects.Models.Project { Name = "Project" };
        var stream = new Backend.Modules.Projects.Models.Stream { Project = project };
        var task = new AcpTask { Title = "No channel", StreamId = stream.Id };
        _db.AddRange(project, stream, task);
        await _db.SaveChangesAsync();

        await _service.SyncCommentToMessagingAsync(task.Id, "Alice", "Ignored");

        _messaging.Verify(m => m.PostThreadMessageAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _messaging.Verify(m => m.ReplyToThreadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}