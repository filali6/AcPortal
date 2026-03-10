using Backend.Data;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Backend.Kafka;

public class OutboxPublisherService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OutboxPublisherService> _logger;

    public OutboxPublisherService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<OutboxPublisherService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisherService démarré.");

        // Configuration du Producer Kafka
        var config = new ProducerConfig
        {
            BootstrapServers = _configuration["Kafka:BootstrapServers"]
        };

        var topic = _configuration["Kafka:TopicName"];

        using var producer = new ProducerBuilder<string, string>(config).Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(producer, topic!, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur dans OutboxPublisherService.");
            }

            // Attendre 5 secondes avant de relire l'Outbox
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _logger.LogInformation("OutboxPublisherService arrêté.");
    }

    private async Task ProcessOutboxMessagesAsync(
        IProducer<string, string> producer,
        string topic,
        CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 1. Chercher tous les messages non traités
        //    orderby CreatedAt pour respecter l'ordre d'insertion
        var pendingMessages = await db.OutboxMessages
            .Where(m => !m.IsProcessed && m.Retries < 5)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(stoppingToken);

        if (!pendingMessages.Any()) return;

        _logger.LogInformation(
            "{Count} message(s) en attente dans l'Outbox.",
            pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            try
            {
                // 2. Construire le JSON à publier sur Kafka
                var payload = JsonSerializer.Serialize(new
                {
                    toolName = message.ToolName,
                    eventType = message.EventType
                });

                // 3. Publier sur Kafka
                await producer.ProduceAsync(topic, new Message<string, string>
                {
                    Key = message.Id.ToString(),
                    Value = payload
                }, stoppingToken);

                // 4. Marquer comme traité
                message.IsProcessed = true;
                message.ProcessedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Message publié sur Kafka : {ToolName} / {EventType}",
                    message.ToolName, message.EventType);
            }
            catch (Exception ex)
            {
                // 5. En cas d'échec → incrémenter le compteur de tentatives
                message.Retries++;
                _logger.LogError(ex,
                    "Échec publication message {Id} — tentative {Retries}/5",
                    message.Id, message.Retries);
            }
        }

        // 6. Sauvegarder tous les changements en une seule fois
        await db.SaveChangesAsync(stoppingToken);
    }
}