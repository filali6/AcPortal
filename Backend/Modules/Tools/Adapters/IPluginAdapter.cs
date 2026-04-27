namespace Backend.Modules.Tools.Adapters;

public interface IPluginAdapter
{
    string PluginId { get; }
    string GetAccessUrl();
}