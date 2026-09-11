using Backend.Data;
using Backend.Modules.Messaging.Services;
using Backend.Modules.Notifications.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace Backend.Modules.Messaging.Controllers;

[ApiController]
[Route("api/messaging")]
public class MessagingWebhookController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<MessagingWebhookController> _logger;
    private readonly IMessagingProvider _messaging;
    private readonly NotificationService _notificationService;
    private readonly EmailService _emailService;
    private readonly IServiceScopeFactory _scopeFactory;

    // Déduplication des events Slack (Slack retransmet si pas de 200 OK rapide)
    private static readonly ConcurrentDictionary<string, DateTime> ProcessedEventIds = new();

    public MessagingWebhookController(
        AppDbContext db,
        IConfiguration config,
        ILogger<MessagingWebhookController> logger,
        IMessagingProvider messaging,
        NotificationService notificationService,
        EmailService emailService,
        IServiceScopeFactory scopeFactory)
    {
        _db = db;
        _config = config;
        _logger = logger;
        _messaging = messaging;
        _notificationService = notificationService;
        _emailService = emailService;
        _scopeFactory = scopeFactory;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveEvent()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync();

        // 1. Vérification de la signature Slack
        if (!VerifySlackSignature(rawBody))
        {
            _logger.LogWarning("Invalid Slack signature on webhook");
            return Unauthorized();
        }

        var json = JsonDocument.Parse(rawBody);
        var root = json.RootElement;

        // 2. Handshake initial Slack (URL verification)
        if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "url_verification")
        {
            var challenge = root.GetProperty("challenge").GetString();
            return Content(challenge, "text/plain");
        }

        // 3. Déduplication — Slack peut retransmettre le même event_id plusieurs fois
        if (root.TryGetProperty("event_id", out var eventIdProp))
        {
            var eventId = eventIdProp.GetString();
            if (eventId != null)
            {
                CleanupOldEventIds();
                if (!ProcessedEventIds.TryAdd(eventId, DateTime.UtcNow))
                {
                    _logger.LogInformation("Duplicate Slack event ignored: {EventId}", eventId);
                    return Ok();
                }
            }
        }

        // 4. Événement réel
        if (root.TryGetProperty("event", out var eventProp))
        {
            var eventType = eventProp.TryGetProperty("type", out var et) ? et.GetString() : null;

            var botId = eventProp.TryGetProperty("bot_id", out var bid) ? bid.GetString() : null;
            if (botId != null) return Ok();

            if (eventType == "message")
            {
                var channelId = eventProp.TryGetProperty("channel", out var ch) ? ch.GetString() : null;
                var text = eventProp.TryGetProperty("text", out var t) ? t.GetString() : null;
                var userId = eventProp.TryGetProperty("user", out var u) ? u.GetString() : null;
                var threadTs = eventProp.TryGetProperty("thread_ts", out var tts) ? tts.GetString() : null;

                if (text != null && channelId != null)
                {
                    // On répond à Slack immédiatement (200 OK), traitement réel en arrière-plan
                    // pour éviter que Slack ne considère la requête comme échouée et la retransmette.
                    _ = ProcessInBackgroundAsync(threadTs, channelId, text, userId);
                }
            }
        }

        return Ok();
    }

    private async Task ProcessInBackgroundAsync(string? threadTs, string channelId, string text, string? userId)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;

            var db = sp.GetRequiredService<AppDbContext>();
            var messaging = sp.GetRequiredService<IMessagingProvider>();
            var notificationService = sp.GetRequiredService<NotificationService>();
            var emailService = sp.GetRequiredService<EmailService>();
            var logger = sp.GetRequiredService<ILogger<MessagingWebhookController>>();

            var processor = new MessagingEventProcessor(db, messaging, notificationService, emailService, logger);

            if (threadTs != null)
                await processor.HandleThreadReplyAsync(threadTs, text, userId);
            else
                await processor.HandleChannelMessageAsync(channelId, text, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background Slack event processing failed");
        }
    }

    private static void CleanupOldEventIds()
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        foreach (var key in ProcessedEventIds.Where(kv => kv.Value < cutoff).Select(kv => kv.Key).ToList())
        {
            ProcessedEventIds.TryRemove(key, out _);
        }
    }

    private bool VerifySlackSignature(string rawBody)
    {
        var signingSecret = _config["Messaging:Slack:SigningSecret"];
        if (string.IsNullOrEmpty(signingSecret)) return false;

        if (!Request.Headers.TryGetValue("X-Slack-Signature", out var slackSignature)) return false;
        if (!Request.Headers.TryGetValue("X-Slack-Request-Timestamp", out var timestamp)) return false;

        var baseString = $"v0:{timestamp}:{rawBody}";

        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(signingSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
        var computedSignature = "v0=" + Convert.ToHexString(hash).ToLower();

        return computedSignature == slackSignature.ToString();
    }
}

// ===== Logique métier extraite dans une classe à part, instanciable avec un scope DI propre =====
public class MessagingEventProcessor
{
    private readonly AppDbContext _db;
    private readonly IMessagingProvider _messaging;
    private readonly NotificationService _notificationService;
    private readonly EmailService _emailService;
    private readonly ILogger _logger;

    public MessagingEventProcessor(
        AppDbContext db,
        IMessagingProvider messaging,
        NotificationService notificationService,
        EmailService emailService,
        ILogger logger)
    {
        _db = db;
        _messaging = messaging;
        _notificationService = notificationService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task HandleThreadReplyAsync(string threadTs, string text, string? slackUserId)
    {
        var task = await _db.AcpTasks.FirstOrDefaultAsync(t => t.MessagingThreadId == threadTs);
        if (task == null)
        {
            _logger.LogWarning("No task found for Slack thread {ThreadTs}", threadTs);
            return;
        }

        var authorName = slackUserId != null
            ? await _messaging.GetUserNameAsync(slackUserId)
            : "Slack User";

        _db.TaskComments.Add(new Backend.Modules.Tasks.Models.TaskComment
        {
            TaskId = task.Id,
            AuthorKeycloakId = "slack-bot",
            AuthorName = authorName,
            Content = text,
            CreatedAt = DateTime.UtcNow,
            FromMessaging = true,
            MessagingMessageId = threadTs,
            Mentions = new()
        });

        await _db.SaveChangesAsync();
        _logger.LogInformation("Comment created from Slack reply for task {TaskId}", task.Id);

        var originalAuthor = await _db.TaskComments
            .Where(c => c.TaskId == task.Id && c.ParentCommentId == null && !c.FromMessaging)
            .OrderBy(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (originalAuthor != null && originalAuthor.AuthorKeycloakId != "slack-bot")
        {
            await _notificationService.SendAsync(
                originalAuthor.AuthorKeycloakId,
                $"{authorName} replied via Slack on \"{task.Title}\"",
                $"/tasks/{task.Id}"
            );

            var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == originalAuthor.AuthorKeycloakId);
            if (user?.Email != null)
            {
                await _emailService.SendSlackReplyEmailAsync(user.Email, user.FullName, authorName, task.Title, text);
            }

            _logger.LogInformation("Slack reply notification + email sent to {KeycloakId}", originalAuthor.AuthorKeycloakId);
        }
    }

    public async Task HandleChannelMessageAsync(string channelId, string text, string? slackUserId)
    {
        var stream = await _db.Streams
            .Include(s => s.Members).ThenInclude(m => m.Consultant)
            .Include(s => s.BusinessTeamLead)
            .Include(s => s.TechnicalTeamLead)
            .FirstOrDefaultAsync(s => s.MessagingChannelId == channelId);

        if (stream == null)
        {
            _logger.LogWarning("No stream found for Slack channel {ChannelId}", channelId);
            return;
        }

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == stream.ProjectId);
        var authorName = slackUserId != null
            ? await _messaging.GetUserNameAsync(slackUserId)
            : "Slack User";

        var recipients = new List<(string Email, string Name)>();

        if (stream.BusinessTeamLead?.Email != null)
            recipients.Add((stream.BusinessTeamLead.Email, stream.BusinessTeamLead.FullName));

        if (stream.TechnicalTeamLead?.Email != null)
            recipients.Add((stream.TechnicalTeamLead.Email, stream.TechnicalTeamLead.FullName));

        recipients.AddRange(
            stream.Members
                .Where(m => m.Consultant?.Email != null)
                .Select(m => (m.Consultant.Email!, m.Consultant.FullName)));

        if (project?.ProjectManagerId != null)
        {
            var pm = await _db.Users.FirstOrDefaultAsync(u => u.Id == project.ProjectManagerId.Value);
            if (pm?.Email != null) recipients.Add((pm.Email, pm.FullName));
        }

        recipients = recipients
            .GroupBy(r => r.Email)
            .Select(g => g.First())
            .ToList();

        foreach (var (email, name) in recipients)
        {
            await _emailService.SendSlackChannelMessageEmailAsync(email, name, authorName, stream.Name, text);
        }

        _logger.LogInformation(
            "Channel message email sent to {Count} recipients for stream {StreamId}",
            recipients.Count, stream.Id);
    }
}