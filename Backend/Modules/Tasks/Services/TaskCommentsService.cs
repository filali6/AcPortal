using Backend.Data;
using Backend.Modules.Tasks.Models;
using Backend.Modules.Notifications.Services;
using Backend.Modules.Teams.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Tasks.Services;

public class TaskCommentsService
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notificationService;
    private readonly TeamsNotificationService _teamsService;

    private readonly TeamsCommentSyncService _teamsCommentSync;
    private readonly EmailService _emailService;

    public TaskCommentsService(
    AppDbContext db,
    NotificationService notificationService,TeamsNotificationService teamsService,TeamsCommentSyncService teamsCommentSync,EmailService emailService)
    {
        _db = db;
        _notificationService = notificationService;
        _teamsService = teamsService;
        _teamsCommentSync = teamsCommentSync;
        _emailService = emailService;
    }

    public async Task<List<object>> GetCommentsAsync(Guid taskId)
    {
        var comments = await _db.TaskComments
            .Where(c => c.TaskId == taskId && c.ParentCommentId == null)
            .Include(c => c.Replies)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.Content,
                c.AuthorName,
                c.AuthorKeycloakId,
                c.CreatedAt,
                c.Mentions,
                replies = c.Replies.OrderBy(r => r.CreatedAt).Select(r => new
                {
                    r.Id,
                    r.Content,
                    r.AuthorName,
                    r.AuthorKeycloakId,
                    r.CreatedAt,
                    r.Mentions
                })
            })
            .ToListAsync<object>();

        return comments;
    }

    public async Task<TaskComment> AddCommentAsync(
        Guid taskId,
        string content,
        string authorKeycloakId,
        string authorName,
        Guid? parentCommentId,
        List<string>? mentions)
    {
        var comment = new TaskComment
        {
            Content = content,
            TaskId = taskId,
            AuthorKeycloakId = authorKeycloakId,
            AuthorName = authorName,
            ParentCommentId = parentCommentId,
            Mentions = mentions ?? new List<string>()
        };

        _db.TaskComments.Add(comment);
        await _db.SaveChangesAsync();

        await _teamsCommentSync.SyncCommentToTeamsAsync(taskId, authorName, content);

        // Notifier les membres du stream
        await NotifyStreamMembersAsync(taskId, authorKeycloakId, authorName, content, mentions);

        return comment;
    }

    private async Task NotifyStreamMembersAsync(
        Guid taskId,
        string authorKeycloakId,
        string authorName,
        string content,
        List<string>? mentions)
    {
        // 1. Trouver le stream de cette tâche
        var task = await _db.AcpTasks.FindAsync(taskId);
        if (task == null) return;

        var streamId = task.StreamId;
        if (!streamId.HasValue)
        {
            // Chercher via ProjectStep
            streamId = await _db.ProjectSteps
                .Where(s => s.Id == task.StepId)
                .Select(s => s.StreamId)
                .FirstOrDefaultAsync();
        }

        if (!streamId.HasValue) return;

        // 2. Charger tous les membres du stream
        var stream = await _db.Streams
            .Include(s => s.Members).ThenInclude(m => m.Consultant)
            .Include(s => s.BusinessTeamLead)
            .Include(s => s.TechnicalTeamLead)
            .FirstOrDefaultAsync(s => s.Id == streamId.Value);

        if (stream == null) return;

        // 3. Construire la liste des destinataires
        var recipients = new List<string>();

        if (stream.BusinessTeamLead != null)
            recipients.Add(stream.BusinessTeamLead.KeycloakId);

        if (stream.TechnicalTeamLead != null)
            recipients.Add(stream.TechnicalTeamLead.KeycloakId);

        foreach (var member in stream.Members)
            recipients.Add(member.Consultant.KeycloakId);

         
        var preview = content.Length > 50 ? content[..50] + "..." : content;
        var message = $"{authorName} commented on a task: \"{preview}\"";
        var link = $"/tasks/{taskId}";

        foreach (var recipientId in recipients.Distinct())
        {
            if (recipientId == authorKeycloakId) continue; // pas d'auto-notification
            await _notificationService.SendAsync(recipientId, message, link);
        }

        // 5. Notification spéciale pour les mentions
        if (mentions != null && mentions.Any())
        {
            foreach (var mentionedId in mentions)
            {
                if (mentionedId == authorKeycloakId) continue;
                if (recipients.Contains(mentionedId)) continue; // déjà notifié
                await _notificationService.SendAsync(
                    mentionedId,
                    $"{authorName} mentioned you in a comment",
                    link
                );
            }
        }
        var teamsPreview = content.Length > 50 ? content[..50] + "..." : content;
        await _teamsService.SendAsync(
        $"💬 Nouveau commentaire — {task.Title}",
            $"{authorName} : \"{teamsPreview}\"",
            "http://localhost:4200"
    );
        // Send email to mentioned users
        if (mentions != null && mentions.Any())
        {
            foreach (var mentionedKeycloakId in mentions)
            {
                var mentionedUser = await _db.Users
                    .FirstOrDefaultAsync(u => u.KeycloakId == mentionedKeycloakId);

                if (mentionedUser?.Email != null)
                {
                    await _emailService.SendMentionEmailAsync(
                        mentionedUser.Email,
                        mentionedUser.FullName,
                        authorName,
                        task.Title,
                        content
                    );
                }
            }
        }
    }

    public async Task<bool> DeleteCommentAsync(Guid commentId, Guid taskId, string keycloakId)
    {
        var comment = await _db.TaskComments
            .FirstOrDefaultAsync(c => c.Id == commentId && c.TaskId == taskId);

        if (comment == null) return false;
        if (comment.AuthorKeycloakId != keycloakId) return false;

        _db.TaskComments.Remove(comment);
        await _db.SaveChangesAsync();
        return true;
    }
   
}