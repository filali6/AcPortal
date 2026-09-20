using Dapr.Client;

namespace Backend.Modules.Events.Services;

using System.Text.Json;
using System.Text;
using System.Net.Http;

public class EventPublisher
{
    private readonly DaprClient _dapr;
    private readonly ILogger<EventPublisher> _logger;
    private readonly IConfiguration _configuration;

    public EventPublisher(DaprClient dapr, ILogger<EventPublisher> logger, IConfiguration configuration)
    {
        _dapr = dapr;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task PublishAsync(object payload, Guid? projectId = null, string? projectName = null)
    {
        string topic;

        if (projectId.HasValue && !string.IsNullOrEmpty(projectName))
        {
            var safeName = SanitizeTopicSegment(projectName);
            topic = $"project.{safeName}";
        }
        else
        {
            topic = "system.events";
        }

        var maxRetries = _configuration.GetValue<int>("EventPublisher:MaxRetries", 5);
        var delai = _configuration.GetValue<int>("EventPublisher:RetryDelayMs", 500);
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var httpClient = new HttpClient();
                var encodedTopic = Uri.EscapeDataString(topic);
                var response = await httpClient.PostAsync(
                    $"http://localhost:3500/v1.0/publish/pubsub/{encodedTopic}",
                    content);
                response.EnsureSuccessStatusCode();
                _logger.LogInformation("Event publié → topic : {Topic}", topic);
                return;
            }
            catch (Exception ex)
            {
                await Task.Delay(delai);
                _logger.LogWarning("Essai {Essai} échoué sur {Topic} : {Erreur} | Inner: {Inner}",
                    i + 1, topic, ex.Message, ex.InnerException?.Message ?? "aucune");
            }
        }

        _logger.LogError("Impossible de publier sur {Topic} après {Max} essais", topic, maxRetries);
    }

    private static string SanitizeTopicSegment(string input)
    {
        var cleaned = new string(input
            .ToLower()
            .Replace(" ", "-")
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .ToArray());

        return string.IsNullOrEmpty(cleaned) ? "unknown" : cleaned;
    }
}