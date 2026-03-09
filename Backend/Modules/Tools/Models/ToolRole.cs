namespace Backend.Modules.Tools.Models;

public class ToolRole
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public Guid ToolId { get; set; }
}