using Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Teams.Services;

public class TeamsCommentSyncService
{
    private readonly AppDbContext _db;
    private readonly GraphService _graphService;
    private readonly ILogger<TeamsCommentSyncService> _logger;

    public TeamsCommentSyncService(
        AppDbContext db,
        GraphService graphService,
        ILogger<TeamsCommentSyncService> logger)
    {
        _db = db;
        _graphService = graphService;
        _logger = logger;
    }

    /// <summary>
    /// Called when a comment is posted in ACPortal.
    /// Posts the comment to the correct Teams thread.
    /// Creates the thread if it's the first comment on the task.
    /// </summary>
    public async Task SyncCommentToTeamsAsync(
        Guid taskId,
        string authorName,
        string content)
    {
        // 1. Load the task with its stream and project
        var task = await _db.AcpTasks.FindAsync(taskId);
        if (task == null) return;

        // 2. Find the stream → channel
        var streamId = task.StreamId;
        if (!streamId.HasValue)
        {
            streamId = await _db.ProjectSteps
                .Where(s => s.Id == task.StepId)
                .Select(s => s.StreamId)
                .FirstOrDefaultAsync();
        }
        if (!streamId.HasValue) return;

        var stream = await _db.Streams
            .FirstOrDefaultAsync(s => s.Id == streamId.Value);
        if (stream?.TeamsChannelId == null) return;

        // 3. Find the project → team
        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == stream.ProjectId);
        if (project?.TeamsTeamId == null) return;

        // 4. Format the message
        var message = $"<b>{authorName}</b> on <b>[{task.Title}]</b>: {content}";

        // 5. First comment on task → create new thread
        //    Subsequent comments → reply to existing thread
        if (task.TeamsThreadId == null)
        {
            var threadId = await _graphService.PostMessageAsync(
                project.TeamsTeamId,
                stream.TeamsChannelId,
                message
            );

            if (threadId != null)
            {
                task.TeamsThreadId = threadId;
                await _db.SaveChangesAsync();
                _logger.LogInformation("Created Teams thread for task {TaskId}", taskId);
            }
        }
        else
        {
            await _graphService.ReplyToThreadAsync(
                project.TeamsTeamId,
                stream.TeamsChannelId,
                task.TeamsThreadId,
                message
            );
            _logger.LogInformation("Replied to Teams thread for task {TaskId}", taskId);
        }
    }
}