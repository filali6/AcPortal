using Backend.Data;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
 
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

        var config = new ProducerConfig
        {
            BootstrapServers = _configuration["Kafka:BootstrapServers"],
            AllowAutoCreateTopics = true

        };

 
        var defaultTopic = _configuration["Kafka:TopicName"];

        using var producer = new ProducerBuilder<string, string>(config).Build();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(producer, defaultTopic!, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur dans OutboxPublisherService.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _logger.LogInformation("OutboxPublisherService arrêté.");
    }

    private async Task ProcessOutboxMessagesAsync(
        IProducer<string, string> producer,
        string defaultTopic,   
        CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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
                
                var topic = !string.IsNullOrEmpty(message.Topic)
                ? message.Topic
                : defaultTopic;

                var payload = message.Payload;

                await producer.ProduceAsync(topic, new Message<string, string>
                {
                    Key = message.Id.ToString(),
                    Value = payload
                }, stoppingToken);

                message.IsProcessed = true;
                message.ProcessedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Message publié — topic : {Topic}",
                    topic);
            }
            catch (Exception ex)
            {
                message.Retries++;
                _logger.LogError(ex,
                    "Échec publication message {Id} — tentative {Retries}/5",
                    message.Id, message.Retries);
            }
        }

        await db.SaveChangesAsync(stoppingToken);
    }
}