namespace Backend.Modules.Tools.Adapters;

public class BudibaseAdapter : IPluginAdapter
{
    public string PluginId => "budibase";

    public string GetAccessUrl()
    {
        return Environment.GetEnvironmentVariable("PLUGIN_BUDIBASE_URL")
               ?? "http://localhost:3002";
    }
}