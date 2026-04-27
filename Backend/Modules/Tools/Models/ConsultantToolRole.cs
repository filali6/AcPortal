namespace Backend.Modules.Tools.Models;

public class ConsultantToolRole
{
    public Guid Id { get; set; } = Guid.NewGuid();

    
    public Guid ConsultantId { get; set; }

     
    public Guid ToolId { get; set; }

     
    public Guid ToolRoleId { get; set; }

    
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}