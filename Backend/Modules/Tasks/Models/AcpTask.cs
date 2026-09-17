//using Backend.Modules.Sla.Models;

namespace Backend.Modules.Tasks.Models;

public enum AcpTaskStatus
{
    Pending,  
    Blocked,   
    Done        
}

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

     public Guid ?SourceEventId { get; set; }

    public Guid? ProjectId { get; set; }
    public Guid? StepId { get; set; }
    public Guid? StreamId { get; set; }

    public Guid? ContractId { get; set; }
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();

    public string? MessagingThreadId { get; set; }
    public string? MessagingThreadUrl { get; set; }
    // Ajouter dans AcpTask.cs
    public DateTime? DueDate { get; set; }
    public Guid? SlaRuleId { get; set; }
    //public SlaStatus SlaStatus { get; set; } = SlaStatus.OnTrack;


}