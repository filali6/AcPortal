using System.Text;
using System.Text.Json;

namespace Backend.Modules.Notifications.Services;

public class TeamsNotificationService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public TeamsNotificationService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task SendAsync(string title, string message, string? link = null, string? webhookUrl = null)
    {
        var url = webhookUrl ?? _config["Teams:DefaultWebhookUrl"];
        if (string.IsNullOrEmpty(url)) return;

        var body = new
        {
            type = "message",
            attachments = new[]
            {
                new
                {
                    contentType = "application/vnd.microsoft.card.adaptive",
                    content = new
                    {
                        type = "AdaptiveCard",
                        version = "1.4",
                        body = new object[]
                        {
                            new { type = "TextBlock", size = "Medium", weight = "Bolder", text = title },
                            new { type = "TextBlock", text = message, wrap = true }
                        },
                        actions = link == null ? null : new object[]
                        {
                            new { type = "Action.OpenUrl", title = "Voir dans ACPortal", url = link }
                        }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(body);
        await _http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
    }
}