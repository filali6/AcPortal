using System.Text.Json;
using Backend.Modules.Tools.Adapters;
using Backend.Modules.Tools.Models;

namespace Backend.Modules.Tools.Services;

public class PluginRegistry
{
    private readonly List<PluginDefinition> _definitions = new();
    private readonly Dictionary<string, IPluginAdapter> _adapters = new();

    public PluginRegistry()
    {
        // Enregistrer les adapters
        Register(new GiteaAdapter());
        Register(new BudibaseAdapter());

        // Lire les fichiers JSON
        LoadDefinitions();
    }

    private void Register(IPluginAdapter adapter)
    {
        _adapters[adapter.PluginId] = adapter;
    }

    private void LoadDefinitions()
    {
        var pluginsPath = Path.Combine(Directory.GetCurrentDirectory(), "plugins");
        if (!Directory.Exists(pluginsPath)) return;

        foreach (var file in Directory.GetFiles(pluginsPath, "*.json"))
        {
            var json = File.ReadAllText(file);
            var definition = JsonSerializer.Deserialize<PluginDefinition>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (definition != null)
                _definitions.Add(definition);
        }
    }

    public List<PluginDefinition> GetAll() => _definitions;

    public PluginDefinition? GetById(string pluginId) =>
        _definitions.FirstOrDefault(d => d.Id == pluginId);

    public IPluginAdapter? GetAdapter(string pluginId) =>
        _adapters.TryGetValue(pluginId, out var adapter) ? adapter : null;
}