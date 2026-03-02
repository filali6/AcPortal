namespace Backend.Modules.Events.Models;

public class AcpEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
 
    public string ToolName { get; set; } = string.Empty;

   
    public string EventType { get; set; } = string.Empty;

    

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    public Guid? GeneratedTaskId { get; set; }
}