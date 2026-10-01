using Dapr.Client;
using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.PublishSubscribe.Extensions;
using System.Collections.Concurrent;
using System.Text.Json;
using Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Events.Services;

/// <summary>
/// Gère les abonnements Dapr (streaming subscriptions).
/// Un "watchdog" vérifie toutes les 20 s, via l'API metadata de Dapr,
/// que les abonnements existent vraiment. S'ils ont disparu (backend démarré
/// avant Dapr, Dapr redémarré, PC redémarré...), il se réabonne tout seul.
/// </summary>
public class StreamingSubscriptionService : IHostedService
{
    private const string SystemTopic = "system.events";
    private const string DaprMetadataUrl = "http://localhost:3500/v1.0/metadata";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(20);

    private readonly DaprPublishSubscribeClient _pubsubClient;
    private readonly DaprClient _daprClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StreamingSubscriptionService> _logger;

    private readonly Dictionary<string, IAsyncDisposable> _subscriptions = new();
    private readonly ConcurrentDictionary<string, byte> _readyTopics = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private CancellationTokenSource? _cts;
    private Task? _watchdog;

    public StreamingSubscriptionService(
        DaprPublishSubscribeClient pubsubClient,
        DaprClient daprClient,
        IServiceProvider serviceProvider,
        ILogger<StreamingSubscriptionService> logger)
    {
        _pubsubClient = pubsubClient;
        _daprClient = daprClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // Ne bloque pas le démarrage de l'app : tout se passe en arrière-plan.
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        _watchdog = Task.Run(() => WatchdogLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        if (_watchdog != null)
        {
            try { await _watchdog; } catch { /* arrêt */ }
        }
        await DisposeAllSubscriptionsAsync();
    }

    private async Task WatchdogLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // 1. Attendre que le sidecar Dapr réponde
                await _daprClient.WaitForSidecarAsync(ct);

                // 2. Vérifier que Dapr a VRAIMENT nos abonnements
                if (!await DaprHasSubscriptionAsync(SystemTopic, ct))
                {
                    _logger.LogWarning("Abonnements absents cote Dapr -> (re)abonnement de tous les topics");
                    await SubscribeAllAsync(ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Watchdog abonnements : erreur, nouvel essai dans {Delay}s",
                    CheckInterval.TotalSeconds);
            }

            try { await Task.Delay(CheckInterval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task<bool> DaprHasSubscriptionAsync(string topic, CancellationToken ct)
    {
        var json = await _http.GetStringAsync(DaprMetadataUrl, ct);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("subscriptions", out var subs) ||
            subs.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var sub in subs.EnumerateArray())
        {
            if (sub.TryGetProperty("topic", out var t) && t.GetString() == topic)
                return true;
        }
        return false;
    }

    private async Task SubscribeAllAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await DisposeAllSubscriptionsCoreAsync();

            await SubscribeToTopicCoreAsync(SystemTopic, ct);
            _logger.LogInformation("Abonné à system.events");

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var projectNames = await db.Projects.Select(p => p.Name).ToListAsync(ct);

            // Distinct : évite les doublons (ex : plusieurs projets "test")
            var topics = projectNames
                .Select(name => $"project.{SanitizeTopicSegment(name)}")
                .Distinct()
                .ToList();

            foreach (var topic in topics)
            {
                await SubscribeToTopicCoreAsync(topic, ct);
                _logger.LogInformation("Abonné dynamiquement à : {Topic}", topic);
            }

            _logger.LogInformation("Abonné à {Count} topics projets existants", topics.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    // Appelée par le reste du code quand un projet est créé
    public async Task SubscribeToProjectAsync(string projectName)
    {
        var topic = $"project.{SanitizeTopicSegment(projectName)}";

        await _lock.WaitAsync();
        try
        {
            if (_subscriptions.ContainsKey(topic))
                return;

            await SubscribeToTopicCoreAsync(topic, _cts?.Token ?? CancellationToken.None);
        }
        finally
        {
            _lock.Release();
        }

        _logger.LogInformation("Abonné dynamiquement à : {Topic}", topic);
    }

    public async Task WaitForTopicAsync(string topic, CancellationToken cancellationToken = default)
    {
        var elapsed = 0;
        while (!_readyTopics.ContainsKey(topic) && elapsed < 3000)
        {
            await Task.Delay(100, cancellationToken);
            elapsed += 100;
        }
    }

    private async Task DisposeAllSubscriptionsAsync()
    {
        await _lock.WaitAsync();
        try { await DisposeAllSubscriptionsCoreAsync(); }
        finally { _lock.Release(); }
    }

    private async Task DisposeAllSubscriptionsCoreAsync()
    {
        foreach (var sub in _subscriptions.Values)
        {
            try { await sub.DisposeAsync(); }
            catch (Exception ex) { _logger.LogDebug(ex, "Erreur a la fermeture d'un abonnement"); }
        }
        _subscriptions.Clear();
        _readyTopics.Clear();
    }

    // Doit rester IDENTIQUE à EventPublisher.SanitizeTopicSegment
    private static string SanitizeTopicSegment(string input)
    {
        var cleaned = new string(input
            .ToLower()
            .Replace(" ", "-")
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .ToArray());

        return string.IsNullOrEmpty(cleaned) ? "unknown" : cleaned;
    }

    private async Task SubscribeToTopicCoreAsync(string topic, CancellationToken cancellationToken)
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
                    JsonElement json;
                    var root = JsonDocument.Parse(jsonString).RootElement;
                    if (root.TryGetProperty("data", out var dataElement))
                        json = dataElement;
                    else
                        json = root;

                    _logger.LogInformation(
                        "Event reçu sur {Topic} : {Payload}", topic, json.ToString());

                    Guid? projectId = null;
                    if (json.TryGetProperty("projectId", out var pid) &&
                        Guid.TryParse(pid.GetString(), out var parsedId))
                        projectId = parsedId;

                    Guid? streamId = null;
                    if (json.TryGetProperty("streamId", out var sid) &&
                        Guid.TryParse(sid.GetString(), out var parsedStreamId))
                        streamId = parsedStreamId;

                    using var scope = _serviceProvider.CreateScope();
                    var processor = scope.ServiceProvider
                        .GetRequiredService<EventProcessorService>();
                    await processor.ProcessAsync(json.ToString(), projectId, streamId);

                    return TopicResponseAction.Success;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur complète sur {Topic}: {Message} \n {StackTrace}",
                        topic, ex.Message, ex.StackTrace);
                    return TopicResponseAction.Retry;
                }
            },
            cancellationToken);

        _subscriptions[topic] = subscription;
        await Task.Delay(300, cancellationToken);
        _readyTopics[topic] = 0;
    }
}