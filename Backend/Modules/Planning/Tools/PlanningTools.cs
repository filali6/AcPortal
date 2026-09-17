using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Tools.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;

namespace Backend.Modules.Planning.Tools;

public class PlanningTools
{
    private readonly AppDbContext _db;
    private readonly PluginRegistry _plugins;

    public PlanningTools(AppDbContext db, PluginRegistry plugins)
    {
        _db = db;
        _plugins = plugins;
    }

    // Tool 1 — tout en un seul appel
    [KernelFunction("get_planning_context")]
    [Description("Returns ALL data needed for planning in one call: eligible plugins with their team type, business leads, technical leads, business consultants, and technical consultants with their current workload.")]
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

        var bizLeads = await _db.Users
            .Where(u => u.Role == GlobalRole.BusinessTeamLead)
            .Select(u => new
            {
                id = u.Id,
                name = u.FullName,
                role = "BusinessTeamLead",
                activeStreams = _db.Streams.Count(s => s.BusinessTeamLeadId == u.Id)
            })
            .ToListAsync();

        var techLeads = await _db.Users
            .Where(u => u.Role == GlobalRole.TechnicalTeamLead)
            .Select(u => new
            {
                id = u.Id,
                name = u.FullName,
                role = "TechnicalTeamLead",
                activeStreams = _db.Streams.Count(s => s.TechnicalTeamLeadId == u.Id)
            })
            .ToListAsync();

        var bizConsultants = await _db.Users
            .Where(u => u.Role == GlobalRole.Consultant
                     && u.ConsultantType == ConsultantType.Business)
            .Select(u => new
            {
                id = u.Id,
                name = u.FullName,
                type = "Business",
                activeStreams = _db.StreamMembers.Count(sm => sm.ConsultantId == u.Id)
            })
            .ToListAsync();

        var techConsultants = await _db.Users
            .Where(u => u.Role == GlobalRole.Consultant
                     && u.ConsultantType == ConsultantType.Technical)
            .Select(u => new
            {
                id = u.Id,
                name = u.FullName,
                type = "Technical",
                activeStreams = _db.StreamMembers.Count(sm => sm.ConsultantId == u.Id)
            })
            .ToListAsync();

        return JsonSerializer.Serialize(new
        {
            plugins,
            businessLeads = bizLeads,
            technicalLeads = techLeads,
            businessConsultants = bizConsultants,
            technicalConsultants = techConsultants
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

                // Valide les plugins
                if (stream.TryGetProperty("steps", out var steps))
                    foreach (var step in steps.EnumerateArray())
                        if (step.TryGetProperty("pluginId", out var pid))
                            if (!eligiblePluginIds.Contains(pid.GetString() ?? ""))
                                errors.Add($"Stream '{streamName}': plugin '{pid}' not eligible.");

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