using Dapr.Client;

namespace Backend.Modules.Events.Services;

using System.Text.Json;
// using System.Text;
 
// using System.Net.Http;

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
            var safeName = projectName
                .ToLower()
                .Replace(" ", "-");
            topic = $"project.{safeName}";
        }
        else
        {
            topic = "system.events";
        }

        // var json = JsonSerializer.Serialize(payload);
        // var content = new StringContent(json, Encoding.UTF8, "application/json");
        // var httpClient = new HttpClient();
        // var response = await httpClient.PostAsync(
        //     $"http://localhost:3500/v1.0/publish/pubsub/{topic}",
        //     content);

        // _logger.LogInformation("Event publié → topic : {Topic} → {Status}",
        //     topic, response.StatusCode);
        var maxRetries = _configuration.GetValue<int>("EventPublisher:MaxRetries", 5);
        var delai = _configuration.GetValue<int>("EventPublisher:RetryDelayMs", 500);
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await _dapr.PublishEventAsync("pubsub", topic, payload);
                _logger.LogInformation("Event publié → topic : {Topic}", topic);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Essai {Essai} échoué sur {Topic}, on réessaie...", i + 1, topic);
                await Task.Delay(delai);
            }
        }

        _logger.LogError("Impossible de publier sur {Topic} après {Max} essais", topic, maxRetries);
    }
}