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

     public string Description { get; set; } = string.Empty;

     public string ToolName { get; set; } = string.Empty;

     public AcpTaskStatus Status { get; set; } = AcpTaskStatus.Pending;

     public string? AssignedTo { get; set; }

     public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

     public DateTime? UpdatedAt { get; set; }

     public Guid ?SourceEventId { get; set; }

    public Guid? ProjectId { get; set; }
    public Guid? StepId { get; set; }

     
}