using System.Text.Json;
using Backend.Modules.Events.Models;

namespace Backend.Modules.Events.Services;

public class WorkflowRulesService
{
    private readonly List<WorkflowRule> _rules;
    private readonly ILogger<WorkflowRulesService> _logger;

    public WorkflowRulesService(ILogger<WorkflowRulesService> logger)
    {
        _logger = logger;
        _rules = LoadRules();
    }

    private List<WorkflowRule> LoadRules()
    {
        // cherche le fichier à la racine du build
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "workflow-config.json"
        );

        if (!File.Exists(path))
        {
            _logger.LogError(
                "workflow-config.json introuvable à : {Path}", path);
            return new List<WorkflowRule>();
        }

        var json = File.ReadAllText(path);

        var config = JsonSerializer.Deserialize<WorkflowConfig>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        var rules = config?.WorkflowRules ?? new List<WorkflowRule>();

        _logger.LogInformation(
            "{Count} règles workflow chargées depuis workflow-config.json",
            rules.Count);

        return rules;
    }

    // retourne la règle pour un eventCode donné
    // retourne null si aucune règle trouvée
    public WorkflowRule? GetRule(string eventCode)
    {
        return _rules.FirstOrDefault(r =>
            r.EventCode.Equals(
                eventCode,
                StringComparison.OrdinalIgnoreCase));
    }

    public List<WorkflowRule> GetAllRules() => _rules;
}