namespace Backend.Modules.Tools.Adapters;

public class GiteaAdapter : IPluginAdapter
{
    public string PluginId => "gitea";

    public string GetAccessUrl()
    {
        return Environment.GetEnvironmentVariable("PLUGIN_GITEA_URL")
               ?? "http://localhost:3001";
    }
}