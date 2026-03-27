namespace Backend.Modules.Events.Models;

public class AcpEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Quel outil a publié cet événement ? ex: "axeIAM", "axeBPM"
    public string ToolName { get; set; } = string.Empty;

    // Quel type d'événement ? ex: "USER_CREATED", "ROLE_UPDATED"
    public string EventType { get; set; } = string.Empty;

    // Le contenu brut reçu de Kafka (JSON)
    public string Payload { get; set; } = string.Empty;

    // Quand on l'a reçu
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    // ID de la tâche générée depuis cet événement (null si pas encore traitée)
    public Guid? GeneratedTaskId { get; set; }
}