namespace Backend.Modules.Tools.Models;

public class PluginDefinition
{
    public Guid DbId { get; set; } = Guid.NewGuid();
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string AdapterType { get; set; } = "generic";
    public bool SsoEnabled { get; set; }
    public bool IsActive { get; set; } = true;
    public string AllowedRoles { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}