using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Backend.Modules.Notifications.Services;

public class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    // ===== Helper interne pour éviter de répéter la connexion SMTP partout =====
    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        try
        {
            var host = _config["Email:Host"]!;
            var port = int.Parse(_config["Email:Port"]!);
            var username = _config["Email:Username"]!;
            var password = _config["Email:Password"]!;

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("ACPortal", username));
            message.To.Add(new MailboxAddress(toName, toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(username, password);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {Email} — {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} — {Subject}", toEmail, subject);
        }
    }

    private static string BuildTemplate(string headerColor, string bodyHtml) => $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
            <div style='background: {headerColor}; padding: 20px; border-radius: 8px 8px 0 0;'>
                <h2 style='color: white; margin: 0;'>ACPortal</h2>
            </div>
            <div style='background: #f8fafc; padding: 24px; border-radius: 0 0 8px 8px;'>
                {bodyHtml}
            </div>
        </div>";

    private static string BuildButton(string color) => $@"
        <a href='http://localhost:4200' 
           style='background: {color}; color: white; padding: 10px 20px; 
                  border-radius: 6px; text-decoration: none; display: inline-block;'>
            View in ACPortal →
        </a>";

    private static string Truncate(string content, int max = 100) =>
        content.Length > max ? content[..max] + "..." : content;

    // ===== 1. @mention dans un commentaire (déjà existant) =====
    public async Task SendMentionEmailAsync(
        string toEmail,
        string toName,
        string mentionedBy,
        string taskTitle,
        string commentContent)
    {
        var preview = Truncate(commentContent);
        var body = $@"
            <p style='font-size: 16px;'>Hi <b>{toName}</b>,</p>
            <p><b>{mentionedBy}</b> mentioned you in a comment on task <b>{taskTitle}</b>:</p>
            <div style='background: white; border-left: 4px solid #3b82f6; padding: 12px 16px; margin: 16px 0; border-radius: 4px;'>
                <p style='margin: 0; color: #374151;'>{preview}</p>
            </div>
            {BuildButton("#3b82f6")}";

        await SendAsync(toEmail, toName, $"💬 {mentionedBy} mentioned you in a comment",
            BuildTemplate("#3b82f6", body));
    }

    // ===== 2. Stream créé → PM + leads + consultants =====
    public async Task SendStreamCreatedEmailAsync(
    string toEmail, string toName, string projectName, string streamName)
    {
        var body = $@"
        <p style='font-size: 16px;'>Hi <b>{toName}</b>,</p>
        <p>A new stream <b>{streamName}</b> has been created on project <b>{projectName}</b>. You're part of this stream.</p>
        <p>Join your team channel to discuss and collaborate:</p>
        {BuildButton("#3b82f6")}";

        await SendAsync(toEmail, toName, $" New stream created: {streamName}",
            BuildTemplate("#3b82f6", body));
    }
    // ===== 3. Commentaire ACPortal sur une tâche → Team Lead(s) du stream =====
    public async Task SendNewCommentEmailAsync(
        string toEmail, string toName, string authorName, string taskTitle, string content)
    {
        var preview = Truncate(content);
        var body = $@"
            <p style='font-size: 16px;'>Hi <b>{toName}</b>,</p>
            <p><b>{authorName}</b> posted a comment on task <b>{taskTitle}</b>:</p>
            <div style='background: white; border-left: 4px solid #3b82f6; padding: 12px 16px; margin: 16px 0; border-radius: 4px;'>
                <p style='margin: 0; color: #374151;'>{preview}</p>
            </div>
            {BuildButton("#3b82f6")}";

        await SendAsync(toEmail, toName, $"💬 New comment on \"{taskTitle}\"",
            BuildTemplate("#3b82f6", body));
    }

    // ===== 4. Réponse dans un thread Slack → auteur original du thread =====
    public async Task SendSlackReplyEmailAsync(
        string toEmail, string toName, string repliedBy, string taskTitle, string content)
    {
        var preview = Truncate(content);
        var body = $@"
            <p style='font-size: 16px;'>Hi <b>{toName}</b>,</p>
            <p><b>{repliedBy}</b> replied via Slack on your task <b>{taskTitle}</b>:</p>
            <div style='background: white; border-left: 4px solid #4A154B; padding: 12px 16px; margin: 16px 0; border-radius: 4px;'>
                <p style='margin: 0; color: #374151;'>{preview}</p>
            </div>
            {BuildButton("#4A154B")}";

        await SendAsync(toEmail, toName, $"💬 {repliedBy} replied via Slack on your task",
            BuildTemplate("#4A154B", body));
    }

    // ===== 5. Message libre dans le channel Slack (hors thread) → tout le stream =====
    public async Task SendSlackChannelMessageEmailAsync(
        string toEmail, string toName, string authorName, string streamName, string content)
    {
        var preview = Truncate(content);
        var body = $@"
            <p style='font-size: 16px;'>Hi <b>{toName}</b>,</p>
            <p><b>{authorName}</b> posted a message in <b>{streamName}</b> on Slack:</p>
            <div style='background: white; border-left: 4px solid #4A154B; padding: 12px 16px; margin: 16px 0; border-radius: 4px;'>
                <p style='margin: 0; color: #374151;'>{preview}</p>
            </div>
            {BuildButton("#4A154B")}";

        await SendAsync(toEmail, toName, $"💬 New message in {streamName} on Slack",
            BuildTemplate("#4A154B", body));
    }
}