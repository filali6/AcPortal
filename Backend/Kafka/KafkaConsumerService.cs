using Confluent.Kafka;
using Backend.Data;
using Backend.Modules.Events.Models;
using Backend.Modules.Tasks.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

        _logger.LogInformation(" Kafka Consumer démarré — écoute le topic : {Topic}", topic);

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(topic);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result == null) continue;

                _logger.LogInformation("Événement reçu : {Message}", result.Message.Value);

                await ProcessEventAsync(result.Message.Value);
                consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, " Erreur lors de la consommation Kafka");
            }
        }

        consumer.Close();
        _logger.LogInformation(" Kafka Consumer arrêté.");
    }

    private async Task ProcessEventAsync(string messageValue)
    {
        try
        {
            // Désérialiser le JSON reçu
            var eventDto = JsonSerializer.Deserialize<AcpEventDto>(messageValue, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (eventDto == null) return;

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Éviter les doublons
            var exists = await db.AcpEvents
                .AnyAsync(e => e.ToolName == eventDto.ToolName &&
                               e.EventType == eventDto.EventType &&
                               e.ReceivedAt >= DateTime.UtcNow.AddSeconds(-5));
            if (exists)
            {
                _logger.LogWarning(" Événement doublon ignoré.");
                return;
            }

            // Créer l'événement
            var acpEvent = new AcpEvent
            {
                ToolName = eventDto.ToolName,
                EventType = eventDto.EventType,
                Payload = eventDto.Payload,
                ReceivedAt = DateTime.UtcNow
            };

            // Créer la tâche automatiquement
            var acpTask = new AcpTask
            {
                Title = $"{eventDto.EventType} — {eventDto.ToolName}",
                Description = $"Tâche générée automatiquement depuis {eventDto.ToolName}",
                ToolName = eventDto.ToolName,
                //Status = AcpTaskStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                SourceEventId = acpEvent.Id
            };

            acpEvent.GeneratedTaskId = acpTask.Id;

            db.AcpEvents.Add(acpEvent);
            db.AcpTasks.Add(acpTask);
            await db.SaveChangesAsync();

            _logger.LogInformation(" Tâche créée : {Title}", acpTask.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, " Erreur lors du traitement de l'événement");
        }
    }
}

// DTO pour désérialiser le JSON reçu de Kafka
public class AcpEventDto
{
    public string ToolName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
}