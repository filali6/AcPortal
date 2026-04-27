namespace Backend.Modules.Tools.Models;

public class UserPlugin
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string PluginId { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}