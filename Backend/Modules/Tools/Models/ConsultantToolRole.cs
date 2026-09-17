using Backend.Modules.Auth.Models;
namespace Backend.Modules.Tools.Models;

public class ConsultantToolRole
{
    public Guid Id { get; set; } = Guid.NewGuid();

    
    public Guid ConsultantId { get; set; }
    public User Consultant { get; set; } = null!;



    public Guid ToolId { get; set; }
    public AcpTool Tool { get; set; } = null!;


    public Guid ToolRoleId { get; set; }
    public ToolRole ToolRole { get; set; } = null!;


    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}