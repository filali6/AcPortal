using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.PublishSubscribe.Extensions;
using System.Text.Json;
using Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Events.Services;

public class StreamingSubscriptionService : IHostedService
{
    private readonly DaprPublishSubscribeClient _pubsubClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StreamingSubscriptionService> _logger;
    private readonly List<IAsyncDisposable> _subscriptions = new();
    private readonly HashSet<string> _readyTopics = new();

    public StreamingSubscriptionService(
        DaprPublishSubscribeClient pubsubClient,
        IServiceProvider serviceProvider,
        ILogger<StreamingSubscriptionService> logger)
    {
        _pubsubClient = pubsubClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await SubscribeToTopicAsync("system.events", cancellationToken);
        _logger.LogInformation("Abonné à system.events");
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var projects = await db.Projects.ToListAsync(cancellationToken);

        foreach (var project in projects)
        {
            await SubscribeToProjectAsync(project.Name);
        }

        _logger.LogInformation("Abonné à {Count} topics projets existants", projects.Count);
    }
    public async Task WaitForTopicAsync(string topic)
    {
        var elapsed = 0;
        while (!_readyTopics.Contains(topic) && elapsed < 3000)
        {
            await Task.Delay(100);
            elapsed += 100;
        }
    }


    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var sub in _subscriptions)
            await sub.DisposeAsync();
    }

    public async Task SubscribeToProjectAsync(string projectName)
    {
        var safeName = projectName.ToLower().Replace(" ", "-");
        var topic = $"project.{safeName}";
        await SubscribeToTopicAsync(topic, CancellationToken.None);
        _logger.LogInformation("Abonné dynamiquement à : {Topic}", topic);
    }

    private async Task SubscribeToTopicAsync(string topic, CancellationToken cancellationToken)
    {
        var options = new DaprSubscriptionOptions(
            new MessageHandlingPolicy(
                TimeSpan.FromSeconds(10),
                TopicResponseAction.Retry));

        var subscription = await _pubsubClient.SubscribeAsync(
            "pubsub",
            topic,
            options,
            async (message, token) =>
            {
                try
                {
                    var jsonString = System.Text.Encoding.UTF8.GetString(message.Data.Span);
                    //var json = JsonDocument.Parse(jsonString).RootElement;
                    JsonElement json;
                    var root = JsonDocument.Parse(jsonString).RootElement;
                    if (root.TryGetProperty("data", out var dataElement))
                        json = dataElement;
                    else
                        json = root;

                    _logger.LogInformation(
                        "Event reçu sur {Topic} : {Payload}", topic, json.ToString());
                    Guid? projectId = null;
                    if (json.TryGetProperty("projectId", out var pid))
                    {
                        if (Guid.TryParse(pid.GetString(), out var parsedId))
                            projectId = parsedId;  
                    }
                    using var scope = _serviceProvider.CreateScope();
                    var processor = scope.ServiceProvider
                        .GetRequiredService<EventProcessorService>();
                    await processor.ProcessAsync(json.ToString(), projectId);

                    return TopicResponseAction.Success;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur sur {Topic}", topic);
                    return TopicResponseAction.Retry;
                }
            },
            cancellationToken);

        _subscriptions.Add(subscription);
        await Task.Delay(300);
        _readyTopics.Add(topic);
    }
}