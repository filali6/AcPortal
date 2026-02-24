namespace Backend.Modules.Tasks.Models;

 
 
public class AcpTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

     
    public string Title { get; set; } = string.Empty;

    // Description optionnelle
    public string Description { get; set; } = string.Empty;

    // L'outil concerné, copié depuis l'événement
    public string ToolName { get; set; } = string.Empty;

 

    // À qui la tâche est assignée (optionnel pour l'instant)
    public string? AssignedTo { get; set; }

    // Quand la tâche a été créée
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Quand la tâche a été mise à jour pour la dernière fois
    public DateTime? UpdatedAt { get; set; }

    // Lien vers l'événement qui a généré cette tâche
    public Guid SourceEventId { get; set; }
}