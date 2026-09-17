using Backend.Data;
using Backend.Modules.Tools.Adapters;
using Backend.Modules.Tools.Models;

namespace Backend.Modules.Tools.Services;

public class PluginRegistry
{
    private readonly AppDbContext _db;

    // On injecte la BDD au lieu de lire des fichiers JSON
    public PluginRegistry(AppDbContext db)
    {
        _db = db;
    }

    // Lit depuis la BDD au lieu des fichiers JSON
    public List<PluginDefinition> GetAll()
    {
        return _db.PluginDefinitions.ToList();
    }

    public PluginDefinition? GetById(string pluginId)
    {
        return _db.PluginDefinitions
            .FirstOrDefault(d => d.Id == pluginId);
    }

    public IPluginAdapter? GetAdapter(string pluginId)
    {
        var definition = GetById(pluginId);
        if (definition == null) return null;
        return new GenericAdapter(pluginId, definition.Url);
    }

    // Sauvegarde en BDD au lieu d'écrire un fichier JSON
    public void AddDefinition(PluginDefinition definition)
    {
        var existing = _db.PluginDefinitions
            .FirstOrDefault(d => d.Id == definition.Id);
        if (existing != null)
            _db.PluginDefinitions.Remove(existing);

        _db.PluginDefinitions.Add(definition);
        _db.SaveChanges();
    }

    // Supprime de la BDD au lieu de supprimer un fichier
    public void RemoveDefinition(string pluginId)
    {
        var existing = _db.PluginDefinitions
            .FirstOrDefault(d => d.Id == pluginId);
        if (existing != null)
        {
            _db.PluginDefinitions.Remove(existing);
            _db.SaveChanges();
        }
    }
}