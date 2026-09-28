using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Projects.Services;
using Backend.Modules.Sla.Models;
using Backend.Modules.Tools.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace Backend.Modules.Planning.Tools;

public class PlanningTools
{
    private const int MaxEstimatedDays = 365;

    private readonly AppDbContext _db;
    private readonly PluginRegistry _plugins;
    private readonly ProjectStatusService _projectStatus;

    public PlanningTools(AppDbContext db, PluginRegistry plugins, ProjectStatusService projectStatus)
    {
        _db = db;
        _plugins = plugins;
        _projectStatus = projectStatus;
    }

    // Tool 1 — tout en un seul appel
    [KernelFunction("get_planning_context")]
    [Description("Returns ALL data needed for planning in one call: eligible plugins with their team type, business leads, technical leads, business consultants, and technical consultants with their current workload (number of ACTIVE streams).")]
    public async Task<string> GetPlanningContextAsync()
    {
        var plugins = _plugins.GetAll()
            .Where(p => p.IsActive
                     && !string.IsNullOrEmpty(p.Url)
                     && !string.IsNullOrEmpty(p.FunctionalDomain))
            .Select(p => new
            {
                id = p.Id,
                name = p.Name,
                domain = p.FunctionalDomain,
                teamType = p.FunctionalDomain == "Workflow" ? "Technical" : "Business"
            })
            .ToList();

        // Charge réelle : seulement les streams actifs (un stream terminé ne compte plus)
        var load = await _projectStatus.GetActiveStreamCountByUserAsync();
        int Load(Guid id) => load.TryGetValue(id, out var n) ? n : 0;

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => u.Role == GlobalRole.BusinessTeamLead
                     || u.Role == GlobalRole.TechnicalTeamLead
                     || u.Role == GlobalRole.Consultant)
            .Select(u => new PersonRow(u.Id, u.FullName, u.Role, u.ConsultantType))
            .ToListAsync();

        // Triés du moins chargé au plus chargé
        object People(Func<PersonRow, bool> filter, string label) => users
            .Where(u => filter(u))
            .Select(u => new { id = u.Id, name = u.FullName, role = label, activeStreams = Load(u.Id) })
            .OrderBy(u => u.activeStreams)
            .ToList();

        return JsonSerializer.Serialize(new
        {
            plugins,
            businessLeads = People(u => u.Role == GlobalRole.BusinessTeamLead, "BusinessTeamLead"),
            technicalLeads = People(u => u.Role == GlobalRole.TechnicalTeamLead, "TechnicalTeamLead"),
            businessConsultants = People(u => u.Role == GlobalRole.Consultant
                                           && u.ConsultantType == ConsultantType.Business, "BusinessConsultant"),
            technicalConsultants = People(u => u.Role == GlobalRole.Consultant
                                            && u.ConsultantType == ConsultantType.Technical, "TechnicalConsultant")
        });
    }

    private record PersonRow(Guid Id, string FullName, GlobalRole Role, ConsultantType? ConsultantType);

    // Tool 3 — SLA standard de l'entreprise (référence, pas une contrainte)
    [KernelFunction("get_standard_sla")]
    [Description("Returns the company's SLA rules (maximum calendar days) for tasks and streams, with their functional domain. Use them as a reference for durations, not as a hard limit.")]
    public async Task<string> GetStandardSlaAsync()
    {
        var rules = await _db.SlaRules
            .AsNoTracking()
            .OrderBy(r => r.Type)
            .ThenBy(r => r.FunctionalDomain)
            .Select(r => new
            {
                type = r.Type.ToString(),
                domain = r.FunctionalDomain,
                name = r.Name,
                description = r.Description,
                slaDays = r.SlaDays
            })
            .ToListAsync();

        if (!rules.Any())
            return JsonSerializer.Serialize(new
            {
                rules,
                note = "No SLA rule defined. Estimate durations from the FSD content only."
            });

        return JsonSerializer.Serialize(new
        {
            rules,
            howToUse = "For a step: use the Task rule whose domain equals the plugin's domain; if none, use the Task rule with domain null (default). "
                     + "For a stream: use the Stream rule with domain null (default)."
        });
    }

    // Tool 2 — auto-validation
    [KernelFunction("validate_proposal")]
    [Description("Validates the proposed plan against real ACPortal data. Call this after generating the plan. If validation fails, fix the errors and call this again before returning the final plan.")]
    public async Task<string> ValidateProposalAsync(string proposalJson)
    {
        var errors = new List<string>();

        try
        {
            var plan = JsonSerializer.Deserialize<JsonElement>(proposalJson);
            var streams = plan.GetProperty("streams").EnumerateArray().ToList();

            var eligiblePluginIds = _plugins.GetAll()
                .Where(p => p.IsActive
                         && !string.IsNullOrEmpty(p.Url)
                         && !string.IsNullOrEmpty(p.FunctionalDomain))
                .Select(p => p.Id)
                .ToHashSet();

            var allUserIds = await _db.Users
                .Select(u => u.Id.ToString())
                .ToHashSetAsync();

            foreach (var (stream, index) in streams.Select((s, i) => (s, i)))
            {
                var streamName = stream.TryGetProperty("name", out var n)
                    ? n.GetString() : $"Stream {index + 1}";

                // Valide les steps : plugin + estimatedDays
                if (stream.TryGetProperty("steps", out var steps))
                {
                    foreach (var step in steps.EnumerateArray())
                    {
                        var stepName = step.TryGetProperty("stepName", out var sn)
                            ? sn.GetString() : "?";

                        if (step.TryGetProperty("pluginId", out var pid)
                            && !eligiblePluginIds.Contains(pid.GetString() ?? ""))
                            errors.Add($"Stream '{streamName}': plugin '{pid}' not eligible.");

                        if (!step.TryGetProperty("estimatedDays", out var ed)
                            || ed.ValueKind != JsonValueKind.Number
                            || !ed.TryGetInt32(out var days)
                            || days <= 0)
                        {
                            errors.Add($"Stream '{streamName}', step '{stepName}': 'estimatedDays' is missing or not a positive integer.");
                        }
                        else if (days > MaxEstimatedDays)
                        {
                            errors.Add($"Stream '{streamName}', step '{stepName}': 'estimatedDays' ({days}) is unrealistic (max {MaxEstimatedDays}). Split the step or re-estimate.");
                        }
                    }
                }

                // Valide les consultants Business
                if (stream.TryGetProperty("businessConsultantIds", out var bizCons))
                    foreach (var id in bizCons.EnumerateArray())
                        if (!allUserIds.Contains(id.GetString() ?? ""))
                            errors.Add($"Stream '{streamName}': business consultant '{id}' not found.");

                // Valide les consultants Technical
                if (stream.TryGetProperty("technicalConsultantIds", out var techCons))
                    foreach (var id in techCons.EnumerateArray())
                        if (!allUserIds.Contains(id.GetString() ?? ""))
                            errors.Add($"Stream '{streamName}': technical consultant '{id}' not found.");

                // Valide les leads
                if (stream.TryGetProperty("businessLeadId", out var bl)
                    && bl.ValueKind != JsonValueKind.Null
                    && !allUserIds.Contains(bl.GetString() ?? ""))
                    errors.Add($"Stream '{streamName}': business lead not found.");

                if (stream.TryGetProperty("technicalLeadId", out var tl)
                    && tl.ValueKind != JsonValueKind.Null
                    && !allUserIds.Contains(tl.GetString() ?? ""))
                    errors.Add($"Stream '{streamName}': technical lead not found.");
            }

            if (errors.Any())
                return JsonSerializer.Serialize(new { valid = false, errors });

            return JsonSerializer.Serialize(new { valid = true, streamCount = streams.Count });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                valid = false,
                errors = new[] { $"Invalid JSON: {ex.Message}" }
            });
        }
    }
}