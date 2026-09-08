using Backend.Data;
using Backend.Modules.Tasks.Models;
using Backend.Modules.Teams.Models;
using Backend.Modules.Notifications.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Teams.Controllers;

[ApiController]
[Route("api/teams")]
public class TeamsWebhookController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notificationService;
    private readonly ILogger<TeamsWebhookController> _logger;

    public TeamsWebhookController(
        AppDbContext db,
        NotificationService notificationService,
        ILogger<TeamsWebhookController> logger)
    {
        _db = db;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Receives messages from Teams bot.
    /// Called when someone replies in a Teams thread linked to a task.
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveActivity([FromBody] TeamsActivity activity)
    {
        if (activity?.Type != "message" || activity.Body?.Content == null)
            return Ok();

        if (activity.ReplyToId == null)
            return Ok(); // not a reply — ignore top-level messages

        // 1. Find the task by TeamsThreadId
        var task = await _db.AcpTasks
            .FirstOrDefaultAsync(t => t.TeamsThreadId == activity.ReplyToId);

        if (task == null)
        {
            _logger.LogWarning("No task found for Teams thread {ThreadId}", activity.ReplyToId);
            return Ok();
        }

        // 2. Find the author in our DB by email
        var author = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == activity.From!.Email);

        var authorName = author?.FullName ?? activity.From?.DisplayName ?? "Teams User";
        var authorKeycloak = author?.KeycloakId ?? "teams";

        // 3. Save as a comment in ACPortal
        var comment = new TaskComment
        {
            TaskId = task.Id,
            Content = activity.Body.Content,
            AuthorName = authorName,
            AuthorKeycloakId = authorKeycloak,
            FromTeams = true,
            TeamsMessageId = activity.Id,
            Mentions = new List<string>()
        };

        _db.TaskComments.Add(comment);
        await _db.SaveChangesAsync();

        // 4. Notify stream members via SignalR
        var streamId = task.StreamId;
        if (!streamId.HasValue)
        {
            streamId = await _db.ProjectSteps
                .Where(s => s.Id == task.StepId)
                .Select(s => s.StreamId)
                .FirstOrDefaultAsync();
        }

        if (streamId.HasValue)
        {
            var stream = await _db.Streams
                .Include(s => s.Members).ThenInclude(m => m.Consultant)
                .Include(s => s.BusinessTeamLead)
                .Include(s => s.TechnicalTeamLead)
                .FirstOrDefaultAsync(s => s.Id == streamId.Value);

            if (stream != null)
            {
                var recipients = new List<string>();
                if (stream.BusinessTeamLead != null)
                    recipients.Add(stream.BusinessTeamLead.KeycloakId);
                if (stream.TechnicalTeamLead != null)
                    recipients.Add(stream.TechnicalTeamLead.KeycloakId);
                foreach (var m in stream.Members)
                    recipients.Add(m.Consultant.KeycloakId);

                var message = $"{authorName} replied via Teams on [{task.Title}]";
                foreach (var recipientId in recipients.Distinct())
                {
                    if (recipientId == authorKeycloak) continue;
                    await _notificationService.SendAsync(recipientId, message, null);
                }
            }
        }

        _logger.LogInformation("Teams reply synced to task {TaskId}", task.Id);
        return Ok();
    }
}