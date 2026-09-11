using Backend.Data;
using Backend.Modules.Events.Models;
using Backend.Modules.Notifications.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Events.Handlers;

public class SendCommentEmailHandler : IActionHandler
{
    public string ActionType => "SEND_COMMENT_EMAIL";

    private readonly AppDbContext _db;
    private readonly EmailService _emailService;
    private readonly ILogger<SendCommentEmailHandler> _logger;

    public SendCommentEmailHandler(
        AppDbContext db,
        EmailService emailService,
        ILogger<SendCommentEmailHandler> logger)
    {
        _db = db;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task HandleAsync(WorkflowRule rule, AcpEventDto eventDto, Guid? projectId)
    {
        if (!eventDto.StreamId.HasValue) return;
        if (string.IsNullOrEmpty(eventDto.AuthorName)) return;

        var stream = await _db.Streams
            .Include(s => s.BusinessTeamLead)
            .Include(s => s.TechnicalTeamLead)
            .FirstOrDefaultAsync(s => s.Id == eventDto.StreamId.Value);

        if (stream == null) return;

        var recipients = new List<(string Email, string Name)>();

        if (stream.BusinessTeamLead?.Email != null)
            recipients.Add((stream.BusinessTeamLead.Email, stream.BusinessTeamLead.FullName));

        if (stream.TechnicalTeamLead?.Email != null)
            recipients.Add((stream.TechnicalTeamLead.Email, stream.TechnicalTeamLead.FullName));

        // Le Team Lead ne se notifie pas lui-même s'il est l'auteur du commentaire
        recipients = recipients
            .GroupBy(r => r.Email)
            .Select(g => g.First())
            .ToList();

        foreach (var (email, name) in recipients)
        {
            await _emailService.SendNewCommentEmailAsync(
                email, name, eventDto.AuthorName, eventDto.TaskTitle ?? "a task", eventDto.Content ?? "");
        }

        _logger.LogInformation(
            "Comment email sent to {Count} Team Lead(s) for stream {StreamId}",
            recipients.Count, stream.Id);
    }
}