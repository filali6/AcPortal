using Confluent.Kafka;
using Backend.Modules.Events.Services;

namespace Backend.Kafka;

public class KafkaConsumerService : BackgroundService
{
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;

    public KafkaConsumerService(
        ILogger<KafkaConsumerService> logger,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _configuration["Kafka:BootstrapServers"],
            GroupId = _configuration["Kafka:GroupId"],
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        var topic = _configuration["Kafka:TopicName"];

        _logger.LogInformation("Kafka Consumer démarré — topic : {Topic}", topic);

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result == null) continue;

                _logger.LogInformation("Message reçu depuis Kafka.");

                // Déléguer tout le traitement à EventProcessorService
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider
                    .GetRequiredService<EventProcessorService>();

                await processor.ProcessAsync(result.Message.Value);

                // Confirmer à Kafka que le message est traité
                consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors de la consommation Kafka.");
            }
        }

        consumer.Close();
        _logger.LogInformation("Kafka Consumer arrêté.");
    }
}