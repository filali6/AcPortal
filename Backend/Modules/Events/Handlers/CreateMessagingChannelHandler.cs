using Backend.Data;
using Backend.Modules.Events.Models;
using Backend.Modules.Messaging.Services;
using Backend.Modules.Notifications.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Events.Handlers;

public class CreateMessagingChannelHandler : IActionHandler
{
    public string ActionType => "CREATE_MESSAGING_CHANNEL";

    private readonly AppDbContext _db;
    private readonly IMessagingProvider _messaging;
    private readonly EmailService _emailService;
    private readonly NotificationService _notificationService;
    private readonly ILogger<CreateMessagingChannelHandler> _logger;

    public CreateMessagingChannelHandler(
        AppDbContext db,
        IMessagingProvider messaging,
        EmailService emailService,
        NotificationService notificationService,
        ILogger<CreateMessagingChannelHandler> logger)
    {
        _db = db;
        _messaging = messaging;
        _emailService = emailService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(WorkflowRule rule, AcpEventDto eventDto, Guid? projectId)
    {
        if (!eventDto.StreamId.HasValue) return;

        var stream = await _db.Streams
            .Include(s => s.BusinessTeamLead)
            .Include(s => s.TechnicalTeamLead)
            .Include(s => s.Members).ThenInclude(m => m.Consultant)
            .FirstOrDefaultAsync(s => s.Id == eventDto.StreamId.Value);

        if (stream == null) return;

        if (!string.IsNullOrEmpty(stream.MessagingChannelId)) return; // idempotence

        var project = await _db.Projects
            .Include(p => p.ProjectManager)
            .FirstOrDefaultAsync(p => p.Id == stream.ProjectId);

        if (project == null) return;

        var recipients = new List<(string Email, string Name)>();

        if (stream.BusinessTeamLead?.Email != null)
            recipients.Add((stream.BusinessTeamLead.Email, stream.BusinessTeamLead.FullName));

        if (stream.TechnicalTeamLead?.Email != null)
            recipients.Add((stream.TechnicalTeamLead.Email, stream.TechnicalTeamLead.FullName));

        recipients.AddRange(
            stream.Members
                .Where(m => m.Consultant?.Email != null)
                .Select(m => (m.Consultant.Email!, m.Consultant.FullName)));

        if (project.ProjectManager?.Email != null)
            recipients.Add((project.ProjectManager.Email, project.ProjectManager.FullName));

        recipients = recipients
            .GroupBy(r => r.Email)
            .Select(g => g.First())
            .ToList();

        var memberEmails = recipients.Select(r => r.Email).ToList();

        var recipientKeycloakIds = new List<string>();

        if (stream.BusinessTeamLead != null)
            recipientKeycloakIds.Add(stream.BusinessTeamLead.KeycloakId);

        if (stream.TechnicalTeamLead != null)
            recipientKeycloakIds.Add(stream.TechnicalTeamLead.KeycloakId);

        recipientKeycloakIds.AddRange(
            stream.Members.Where(m => m.Consultant != null).Select(m => m.Consultant.KeycloakId));

        if (project.ProjectManager != null)
            recipientKeycloakIds.Add(project.ProjectManager.KeycloakId);

        recipientKeycloakIds = recipientKeycloakIds.Distinct().ToList();

        var (channelId, channelUrl) = await _messaging.CreateStreamChannelAsync(
            project.Name, stream.Name, memberEmails);

        if (channelId != null)
        {
            stream.MessagingChannelId = channelId;
            stream.MessagingChannelUrl = channelUrl;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Messaging channel created for stream {StreamId}", stream.Id);

            foreach (var (email, name) in recipients)
            {
                await _emailService.SendStreamCreatedEmailAsync(email, name, project.Name, stream.Name);
            }

            foreach (var keycloakId in recipientKeycloakIds)
            {
                await _notificationService.SendAsync(
                    keycloakId,
                    $"You've been assigned to a new stream \"{stream.Name}\" on project \"{project.Name}\"",
                    $"/streams/{stream.Id}"
                );
            }

            _logger.LogInformation(
                "Stream created emails + notifications sent to {Count} recipients for stream {StreamId}",
                recipients.Count, stream.Id);
        }
        else
        {
            _logger.LogWarning("Failed to create messaging channel for stream {StreamId}", stream.Id);
        }
    }
}