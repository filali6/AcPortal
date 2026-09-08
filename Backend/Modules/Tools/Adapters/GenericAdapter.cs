namespace Backend.Modules.Tools.Adapters;

public class GenericAdapter : IPluginAdapter
{
    private readonly string _pluginId;
    private readonly string _url;

    public GenericAdapter(string pluginId, string url)
    {
        _pluginId = pluginId;
        _url = url;
    }

    public string PluginId => _pluginId;
    public string GetAccessUrl() => _url;
}