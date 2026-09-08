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

    public async Task SendMentionEmailAsync(
        string toEmail,
        string toName,
        string mentionedBy,
        string taskTitle,
        string commentContent)
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
            message.Subject = $"💬 {mentionedBy} mentioned you in a comment";

            var preview = commentContent.Length > 100
                ? commentContent[..100] + "..."
                : commentContent;

            message.Body = new TextPart("html")
            {
                Text = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <div style='background: #3b82f6; padding: 20px; border-radius: 8px 8px 0 0;'>
                        <h2 style='color: white; margin: 0;'>ACPortal</h2>
                    </div>
                    <div style='background: #f8fafc; padding: 24px; border-radius: 0 0 8px 8px;'>
                        <p style='font-size: 16px;'>Hi <b>{toName}</b>,</p>
                        <p><b>{mentionedBy}</b> mentioned you in a comment on task <b>{taskTitle}</b>:</p>
                        <div style='background: white; border-left: 4px solid #3b82f6; padding: 12px 16px; margin: 16px 0; border-radius: 4px;'>
                            <p style='margin: 0; color: #374151;'>{preview}</p>
                        </div>
                        <a href='http://localhost:4200' 
                           style='background: #3b82f6; color: white; padding: 10px 20px; 
                                  border-radius: 6px; text-decoration: none; display: inline-block;'>
                            View in ACPortal →
                        </a>
                    </div>
                </div>"
            };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(host, port, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(username, password);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
        }
    }
}