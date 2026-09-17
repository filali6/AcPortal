using Confluent.Kafka;
using Backend.Modules.Events.Services;
using Backend.Data;
using Microsoft.EntityFrameworkCore;

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
            EnableAutoCommit = false,
            AllowAutoCreateTopics = true
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();

        
        await SubscribeToProjectTopics(consumer, stoppingToken);

      
        var lastRefresh = DateTime.UtcNow;

        _logger.LogInformation("Kafka Consumer démarré — écoute tous les topics project.*");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if ((DateTime.UtcNow - lastRefresh).TotalSeconds > 30)
                {
                    await SubscribeToProjectTopics(consumer, stoppingToken);
                    lastRefresh = DateTime.UtcNow;
                }

                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result == null) continue;

                var topicName = result.Topic;
                //var projectIdStr = topicName.Replace("project.", "");
                Guid? projectId = null;
                if (topicName.StartsWith("project."))
                {
                    var projectIdStr = topicName.Replace("project.", "");
                    projectId = Guid.TryParse(projectIdStr, out var pid) ? pid : null;

                }

                _logger.LogInformation(
                    "Message reçu — topic : {Topic} — projectId : {ProjectId}",
                    topicName, projectId);

                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider
                    .GetRequiredService<EventProcessorService>();

                await processor.ProcessAsync(result.Message.Value, projectId);

                consumer.Commit(result);
            }
            catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
            { 
                _logger.LogWarning("Topic pas encore disponible — en attente...");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
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

    
    private async Task SubscribeToProjectTopics(
        IConsumer<string, string> consumer,
        CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var projectIds = await db.Projects
            .Select(p => p.Id.ToString())
            .ToListAsync(stoppingToken);

        
        var topics = projectIds
            .Select(id => $"project.{id}")
            .ToList();

       
        topics.Add("system.events");

        consumer.Subscribe(topics);

        _logger.LogInformation(
            "Abonné à {Count} topics : {Topics}",
            topics.Count,
            string.Join(", ", topics));
    }
}