namespace Backend.Modules.Tools.Models;

public class PluginDefinition
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string BaseUrlEnvKey { get; set; } = string.Empty;
    public string AdapterType { get; set; } = string.Empty;
    public bool SsoEnabled { get; set; }
}