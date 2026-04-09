using Dapr.Client;

namespace Backend.Modules.Events.Services;

using System.Text.Json;
using System.Text;
 
using System.Net.Http;

public class EventPublisher
{
    private readonly DaprClient _dapr;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(DaprClient dapr, ILogger<EventPublisher> logger)
    {
        _dapr = dapr;
        _logger = logger;
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

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var httpClient = new HttpClient();
        var response = await httpClient.PostAsync(
            $"http://localhost:3500/v1.0/publish/pubsub/{topic}",
            content);

        _logger.LogInformation("Event publié → topic : {Topic} → {Status}",
            topic, response.StatusCode);
    }
}