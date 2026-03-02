using Backend.Data;
using Backend.Modules.Events.Models;
using Backend.Modules.Tasks.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Backend.Modules.Events.Services;

public class EventProcessorService
{
    private readonly AppDbContext _db;
    private readonly ILogger<EventProcessorService> _logger;

    public EventProcessorService(AppDbContext db, ILogger<EventProcessorService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ProcessAsync(string messageValue)
    {
        // 1. Désérialiser le JSON
        var eventDto = JsonSerializer.Deserialize<AcpEventDto>(messageValue,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (eventDto == null)
        {
            _logger.LogWarning("Message invalide reçu — impossible de désérialiser.");
            return;
        }

        // 2. Valider les champs obligatoires
        if (string.IsNullOrWhiteSpace(eventDto.ToolName) ||
            string.IsNullOrWhiteSpace(eventDto.EventType))
        {
            _logger.LogWarning("Message ignoré — ToolName ou EventType manquant.");
            return;
        }

        // 3. Vérifier les doublons
        var exists = await _db.AcpEvents.AnyAsync(e =>
            e.ToolName == eventDto.ToolName &&
            e.EventType == eventDto.EventType &&
            e.ReceivedAt >= DateTime.UtcNow.AddSeconds(-5));

        if (exists)
        {
            _logger.LogWarning("Doublon détecté — événement ignoré ({Tool} / {Type}).",
                eventDto.ToolName, eventDto.EventType);
            return;
        }

        // 4. Créer AcpEvent
        var acpEvent = new AcpEvent
        {
            ToolName = eventDto.ToolName,
            EventType = eventDto.EventType,
            //Payload = messageValue,
            ReceivedAt = DateTime.UtcNow
        };

        // 5. Créer AcpTask liée à l'événement
        var acpTask = new AcpTask
        {
            Title = $"{eventDto.EventType} — {eventDto.ToolName}",
            Description = $"Tâche générée automatiquement depuis {eventDto.ToolName}",
            ToolName = eventDto.ToolName,
            //Status = AcpTaskStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            SourceEventId = acpEvent.Id
        };

        // 6. Lier les deux
        acpEvent.GeneratedTaskId = acpTask.Id;

        // 7. Sauvegarder en base
        _db.AcpEvents.Add(acpEvent);
        _db.AcpTasks.Add(acpTask);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Tâche créée avec succès : {Title}", acpTask.Title);
    }
}