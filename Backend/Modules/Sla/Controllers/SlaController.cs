using Backend.Data;
using Backend.Modules.Sla.Models;
using Backend.Modules.Sla.Services;
using Backend.Modules.Tasks.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Sla.Controllers;

[ApiController]
[Route("api/sla")]
public class SlaController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SlaCheckerService _slaChecker;

    public SlaController(AppDbContext db, SlaCheckerService slaChecker)
    {
        _db = db;
        _slaChecker = slaChecker;
    }

    // US64 — Dashboard retards (Responsable CDS)
    [HttpGet("dashboard")]
    [Authorize(Roles = "HeadOfCDS,SuperAdmin")]
    public async Task<IActionResult> GetDashboard()
    {
        var overdueTasks = await _slaChecker.GetOverdueAndAtRiskTasksAsync();
        var overdueStreams = await _slaChecker.GetOverdueAndAtRiskStreamsAsync();

        return Ok(new
        {
            tasks = overdueTasks,
            streams = overdueStreams,
            summary = new
            {
                totalOverdueTasks = overdueTasks.Count(t => t.SlaStatus == SlaStatus.Overdue),
                totalAtRiskTasks = overdueTasks.Count(t => t.SlaStatus == SlaStatus.AtRisk),
                totalOverdueStreams = overdueStreams.Count(s => s.SlaStatus == SlaStatus.Overdue),
                totalAtRiskStreams = overdueStreams.Count(s => s.SlaStatus == SlaStatus.AtRisk)
            }
        });
    }

    // US62 — Tâches du consultant avec délai restant
    [HttpGet("my-tasks")]
    [Authorize(Roles = "Consultant")]
    public async Task<IActionResult> GetMyTasksWithSla()
    {
        var keycloakId = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();

        var now = DateTime.UtcNow;

        var tasks = await _db.AcpTasks
            .Where(t => t.AssignedTo == keycloakId &&
                        t.Status != AcpTaskStatus.Done)
            .OrderBy(t => t.DueDate)
            .ToListAsync();

        var result = tasks.Select(t => new
        {
            t.Id,
            t.Title,
            t.Status,
            t.ToolName,
            t.DueDate,
            t.ProjectId,
            t.StreamId,
            SlaStatus = _slaChecker.GetTaskSlaStatus(t).ToString(),
            DaysRemaining = t.DueDate.HasValue
                ? (int)Math.Round((t.DueDate.Value - now).TotalDays)
                : (int?)null
        });

        return Ok(result);
    }

    // US63 — Définir DueDate d'un stream (Chef de Projet)
    [HttpPatch("streams/{id:guid}/due-date")]
    [Authorize(Roles = "ProjectManager,SuperAdmin")]
    public async Task<IActionResult> SetStreamDueDate(
        Guid id, [FromBody] SetDueDateRequest request)
    {
        var stream = await _db.Streams.FindAsync(id);
        if (stream == null)
            return NotFound(new { message = "Stream not found" });

        stream.DueDate = request.DueDate.HasValue
            ? DateTime.SpecifyKind(request.DueDate.Value, DateTimeKind.Utc)
            : null;

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Due date updated",
            streamId = id,
            dueDate = stream.DueDate
        });
    }

    // NOUVEAU — Modifier la DueDate d'une tâche (Chef de Projet, après approbation)
    [HttpPatch("tasks/{id:guid}/due-date")]
    [Authorize(Roles = "ProjectManager,SuperAdmin")]
    public async Task<IActionResult> SetTaskDueDate(
        Guid id, [FromBody] SetDueDateRequest request)
    {
        var task = await _db.AcpTasks.FindAsync(id);
        if (task == null)
            return NotFound(new { message = "Task not found" });

        if (request.DueDate == null)
            return BadRequest(new { message = "DueDate is required" });

        task.DueDate = DateTime.SpecifyKind(request.DueDate.Value, DateTimeKind.Utc);
        task.SlaRuleId = null; // date fixée à la main, plus liée à une règle
        task.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new { message = "Due date updated", taskId = id, dueDate = task.DueDate });
    }

    // Appliquer règles SLA manuellement (reste disponible, mais c'est automatique maintenant)
    [HttpPost("apply-rules")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> ApplyRules()
    {
        await _slaChecker.ApplySlaRulesAsync();
        return Ok(new { message = "SLA rules applied" });
    }

    // ───────────────────────── Gestion des règles SLA ─────────────────────────

    // GET /api/sla/rules
    [HttpGet("rules")]
    [Authorize(Roles = "HeadOfCDS,SuperAdmin,ProjectManager")]
    public async Task<IActionResult> GetRules()
    {
        var rules = await _db.SlaRules
            .AsNoTracking()
            .OrderBy(r => r.Type)
            .ThenBy(r => r.FunctionalDomain)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Description,
                Type = r.Type.ToString(),
                r.SlaDays,
                r.FunctionalDomain,
                IsDefault = r.FunctionalDomain == null,
                r.CreatedAt
            })
            .ToListAsync();

        return Ok(rules);
    }

    // GET /api/sla/plugin-domains — pour la liste déroulante et les badges du front
    [HttpGet("plugin-domains")]
    [Authorize(Roles = "HeadOfCDS,SuperAdmin,ProjectManager")]
    public IActionResult GetPluginDomains()
    {
        var map = _slaChecker.GetPluginDomains();

        return Ok(new
        {
            domains = map.Values.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(d => d),
            plugins = map.Select(kv => new { pluginId = kv.Key, domain = kv.Value })
        });
    }

    // POST /api/sla/rules
    [HttpPost("rules")]
    [Authorize(Roles = "HeadOfCDS,SuperAdmin")]
    public async Task<IActionResult> CreateRule([FromBody] SlaRuleRequest request)
    {
        var (error, type, domain) = await ValidateRuleAsync(request, excludeId: null);
        if (error != null) return BadRequest(new { message = error });

        var rule = new SlaRule
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            Type = type,
            SlaDays = request.SlaDays,
            FunctionalDomain = domain
        };

        _db.SlaRules.Add(rule);
        await _db.SaveChangesAsync();

        await _slaChecker.ApplySlaRulesAsync(); // effet immédiat sur ce qui n'a pas encore de date

        return Ok(new { message = "Rule created", id = rule.Id });
    }

    // PUT /api/sla/rules/{id}
    [HttpPut("rules/{id:guid}")]
    [Authorize(Roles = "HeadOfCDS,SuperAdmin")]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] SlaRuleRequest request)
    {
        var rule = await _db.SlaRules.FindAsync(id);
        if (rule == null) return NotFound(new { message = "Rule not found" });

        var (error, type, domain) = await ValidateRuleAsync(request, excludeId: id);
        if (error != null) return BadRequest(new { message = error });

        rule.Name = request.Name.Trim();
        rule.Description = request.Description;
        rule.Type = type;
        rule.SlaDays = request.SlaDays;
        rule.FunctionalDomain = domain;

        await _db.SaveChangesAsync();
        await _slaChecker.ApplySlaRulesAsync();

        return Ok(new { message = "Rule updated" });
    }

    // DELETE /api/sla/rules/{id}
    [HttpDelete("rules/{id:guid}")]
    [Authorize(Roles = "HeadOfCDS,SuperAdmin")]
    public async Task<IActionResult> DeleteRule(Guid id)
    {
        var rule = await _db.SlaRules.FindAsync(id);
        if (rule == null) return NotFound(new { message = "Rule not found" });

        _db.SlaRules.Remove(rule);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Rule deleted" });
    }

    // Règles de validation :
    //   - nom obligatoire, jours > 0, type Task ou Stream
    //   - domaine (si rempli) doit exister parmi les plugins
    //   - un seul couple (type + domaine) → donc une seule règle par défaut par type
    private async Task<(string? error, SlaRuleType type, string? domain)> ValidateRuleAsync(
        SlaRuleRequest request, Guid? excludeId)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ("Name is required", default, null);

        if (request.SlaDays <= 0)
            return ("SlaDays must be greater than 0", default, null);

        if (!Enum.TryParse<SlaRuleType>(request.Type, ignoreCase: true, out var type))
            return ("Type must be 'Task' or 'Stream'", default, null);

        var domain = string.IsNullOrWhiteSpace(request.FunctionalDomain)
            ? null
            : request.FunctionalDomain.Trim();

        if (domain != null)
        {
            var knownDomains = _slaChecker.GetPluginDomains().Values;
            var match = knownDomains.FirstOrDefault(d =>
                string.Equals(d, domain, StringComparison.OrdinalIgnoreCase));
            if (match == null)
                return ($"Unknown domain '{domain}'", default, null);
            domain = match; // orthographe exacte du plugin
        }

        var sameTypeRules = await _db.SlaRules
            .AsNoTracking()
            .Where(r => r.Type == type && (excludeId == null || r.Id != excludeId))
            .ToListAsync();

        var duplicate = sameTypeRules.Any(r =>
            string.Equals(r.FunctionalDomain ?? "", domain ?? "", StringComparison.OrdinalIgnoreCase));

        if (duplicate)
            return (domain == null
                ? $"A default {type} rule already exists. Edit it instead."
                : $"A {type} rule already exists for domain '{domain}'. Edit it instead.",
                default, null);

        return (null, type, domain);
    }
}

public class SetDueDateRequest
{
    public DateTime? DueDate { get; set; }
}

public class SlaRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = "Task";
    public int SlaDays { get; set; }
    public string? FunctionalDomain { get; set; }
}