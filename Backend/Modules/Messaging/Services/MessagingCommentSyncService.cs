using Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Messaging.Services;

public class MessagingCommentSyncService
{
    private readonly AppDbContext _db;
    private readonly IMessagingProvider _messaging;
    private readonly ILogger<MessagingCommentSyncService> _logger;

    public MessagingCommentSyncService(
        AppDbContext db,
        IMessagingProvider messaging,
        ILogger<MessagingCommentSyncService> logger)
    {
        _db = db;
        _messaging = messaging;
        _logger = logger;
    }

    public async Task SyncCommentToMessagingAsync(Guid taskId, string authorName, string content)
    {
        var task = await _db.AcpTasks.FindAsync(taskId);
        if (task == null) return;

        var streamId = task.StreamId;
        if (!streamId.HasValue)
        {
            streamId = await _db.ProjectSteps
                .Where(s => s.Id == task.StepId)
                .Select(s => s.StreamId)
                .FirstOrDefaultAsync();
        }
        if (!streamId.HasValue) return;

        var stream = await _db.Streams.FirstOrDefaultAsync(s => s.Id == streamId.Value);
        if (stream?.MessagingChannelId == null) return;

        var message = $"💬 *{authorName}* on [{task.Title}]: {content}";

        if (task.MessagingThreadId == null)
        {
            var (threadId, threadUrl) = await _messaging.PostThreadMessageAsync(
                stream.MessagingChannelId, message);

            if (threadId != null)
            {
                task.MessagingThreadId = threadId;
                task.MessagingThreadUrl = threadUrl;
                await _db.SaveChangesAsync();
                _logger.LogInformation("Created messaging thread for task {TaskId}", taskId);
            }
        }
        else
        {
            await _messaging.ReplyToThreadAsync(stream.MessagingChannelId, task.MessagingThreadId, message);
            _logger.LogInformation("Replied to messaging thread for task {TaskId}", taskId);
        }
    }
}