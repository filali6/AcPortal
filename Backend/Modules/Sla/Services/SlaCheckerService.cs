using Backend.Data;
using Backend.Modules.Projects.Services;
using Backend.Modules.Sla.Models;
using Backend.Modules.Tasks.Models;
using Backend.Modules.Tools.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Sla.Services;

public class SlaCheckerService
{
    private readonly AppDbContext _db;
    private readonly PluginRegistry _plugins;
    private readonly ProjectStatusService _projectStatus;
    private readonly ILogger<SlaCheckerService> _logger;

    public SlaCheckerService(
        AppDbContext db,
        PluginRegistry plugins,
        ProjectStatusService projectStatus,
        ILogger<SlaCheckerService> logger)
    {
        _db = db;
        _plugins = plugins;
        _projectStatus = projectStatus;
        _logger = logger;
    }

    // ───────────────────────── Choix de la règle ─────────────────────────

    // Règle applicable :
    //   1. règle du même type dont le domaine = domaine demandé
    //   2. sinon règle du même type SANS domaine (= règle par défaut)
    public static SlaRule? ResolveRule(
        IEnumerable<SlaRule> rules, SlaRuleType type, string? domain)
    {
        var ofType = rules.Where(r => r.Type == type).ToList();

        if (!string.IsNullOrWhiteSpace(domain))
        {
            var specific = ofType.FirstOrDefault(r =>
                string.Equals(r.FunctionalDomain, domain, StringComparison.OrdinalIgnoreCase));
            if (specific != null) return specific;
        }

        // Anciennes données : s'il reste plusieurs règles générales, on garde la plus courte
        return ofType
            .Where(r => string.IsNullOrWhiteSpace(r.FunctionalDomain))
            .OrderBy(r => r.SlaDays)
            .FirstOrDefault();
    }

    // pluginId → domaine fonctionnel
    public Dictionary<string, string> GetPluginDomains() =>
        _plugins.GetAll()
            .Where(p => !string.IsNullOrEmpty(p.Id) && !string.IsNullOrEmpty(p.FunctionalDomain))
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.First().FunctionalDomain!, StringComparer.OrdinalIgnoreCase);

    // ───────────────────────── Statuts ─────────────────────────

    public SlaStatus GetTaskSlaStatus(AcpTask task)
    {
        if (task.DueDate == null) return SlaStatus.OnTrack;

        var daysRemaining = (task.DueDate.Value - DateTime.UtcNow).TotalDays;

        if (daysRemaining < 0) return SlaStatus.Overdue;
        if (daysRemaining <= 2) return SlaStatus.AtRisk;
        return SlaStatus.OnTrack;
    }

    public SlaStatus GetStreamSlaStatus(DateTime? dueDate)
    {
        if (dueDate == null) return SlaStatus.OnTrack;

        var remaining = (dueDate.Value - DateTime.UtcNow).TotalDays;

        if (remaining < 0) return SlaStatus.Overdue;
        if (remaining <= 3) return SlaStatus.AtRisk;
        return SlaStatus.OnTrack;
    }

    // ───────────────────────── Application automatique ─────────────────────────

    // Donne une DueDate à tout ce qui n'en a pas encore :
    //   - tâches non terminées sans DueDate  → règle Task (domaine du plugin, sinon défaut)
    //   - streams ACTIFS sans DueDate        → règle Stream par défaut
    // Ne touche JAMAIS une DueDate existante (estimation IA ou saisie manuelle).
    public async Task ApplySlaRulesAsync()
    {
        var rules = await _db.SlaRules.AsNoTracking().ToListAsync();
        if (!rules.Any()) return;

        // Tâches
        var domainByPlugin = GetPluginDomains();

        var tasks = await _db.AcpTasks
            .Where(t => t.DueDate == null && t.Status != AcpTaskStatus.Done)
            .ToListAsync();

        var taskCount = 0;
        foreach (var task in tasks)
        {
            domainByPlugin.TryGetValue(task.ToolName ?? "", out var domain);
            var rule = ResolveRule(rules, SlaRuleType.Task, domain);
            if (rule == null) continue;

            task.DueDate = task.CreatedAt.AddDays(rule.SlaDays);
            task.SlaRuleId = rule.Id;
            taskCount++;
        }

        // Streams (seulement les actifs — un stream terminé ne doit pas apparaître "en retard")
        var streamCount = 0;
        var streamRule = ResolveRule(rules, SlaRuleType.Stream, null);

        if (streamRule != null)
        {
            var streams = await _db.Streams
                .Where(s => s.DueDate == null)
                .ToListAsync();

            var activeIds = await _projectStatus.GetActiveStreamIdsAsync(streams.Select(s => s.Id));

            foreach (var stream in streams.Where(s => activeIds.Contains(s.Id)))
            {
                stream.DueDate = stream.CreatedAt.AddDays(streamRule.SlaDays);
                streamCount++;
            }
        }

        if (taskCount + streamCount > 0)
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation("SLA applied: {Tasks} tasks, {Streams} streams", taskCount, streamCount);
        }
    }

    // ───────────────────────── Dashboard (Responsable CDS) ─────────────────────────

    // Tâches en retard / à risque, avec projet, stream, vrai nom de l'assigné et liens Slack
    public async Task<List<TaskSlaItem>> GetOverdueAndAtRiskTasksAsync()
    {
        var now = DateTime.UtcNow;

        var rows = await (
            from t in _db.AcpTasks.AsNoTracking()
            where t.DueDate != null && t.Status != AcpTaskStatus.Done
            join p in _db.Projects on t.ProjectId equals (Guid?)p.Id into pj
            from p in pj.DefaultIfEmpty()
            join s in _db.Streams on t.StreamId equals (Guid?)s.Id into sj
            from s in sj.DefaultIfEmpty()
            join u in _db.Users on t.AssignedTo equals u.KeycloakId into uj
            from u in uj.DefaultIfEmpty()
            select new
            {
                t.Id,
                t.Title,
                t.Status,
                t.DueDate,
                t.AssignedTo,
                AssignedToName = u != null ? u.FullName : null,
                t.ProjectId,
                ProjectName = p != null ? p.Name : null,
                t.StreamId,
                StreamName = s != null ? s.Name : null,
                TaskThreadUrl = t.MessagingThreadUrl,
                StreamChannelUrl = s != null ? s.MessagingChannelUrl : null
            })
            .ToListAsync();

        return rows
            .Select(r => new TaskSlaItem(
                r.Id, r.Title, r.Status, r.DueDate,
                r.AssignedTo, r.AssignedToName,
                r.ProjectId, r.ProjectName,
                r.StreamId, r.StreamName,
                r.TaskThreadUrl, r.StreamChannelUrl,
                GetStreamOrTaskStatus(r.DueDate, atRiskDays: 2),
                (int)Math.Round((r.DueDate!.Value - now).TotalDays)))
            .Where(t => t.SlaStatus != SlaStatus.OnTrack)
            .OrderBy(t => t.DaysRemaining)
            .ToList();
    }

    // Streams ACTIFS en retard / à risque, avec leads et canal Slack
    public async Task<List<StreamSlaItem>> GetOverdueAndAtRiskStreamsAsync()
    {
        var now = DateTime.UtcNow;

        var streams = await _db.Streams
            .AsNoTracking()
            .Where(s => s.DueDate != null)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.DueDate,
                s.ProjectId,
                ProjectName = s.Project.Name,
                BusinessLeadName = s.BusinessTeamLead != null ? s.BusinessTeamLead.FullName : null,
                TechnicalLeadName = s.TechnicalTeamLead != null ? s.TechnicalTeamLead.FullName : null,
                s.MessagingChannelUrl
            })
            .ToListAsync();

        // Un stream terminé n'est jamais "en retard"
        var activeIds = await _projectStatus.GetActiveStreamIdsAsync(streams.Select(s => s.Id));

        return streams
            .Where(s => activeIds.Contains(s.Id))
            .Select(s => new StreamSlaItem(
                s.Id, s.Name, s.DueDate,
                s.ProjectId, s.ProjectName,
                s.BusinessLeadName, s.TechnicalLeadName,
                s.MessagingChannelUrl,
                GetStreamSlaStatus(s.DueDate),
                (int)Math.Round((s.DueDate!.Value - now).TotalDays)))
            .Where(s => s.SlaStatus != SlaStatus.OnTrack)
            .OrderBy(s => s.DaysRemaining)
            .ToList();
    }

    // Même seuils que GetTaskSlaStatus (2 jours), sans avoir besoin de l'entité AcpTask
    private static SlaStatus GetStreamOrTaskStatus(DateTime? dueDate, int atRiskDays)
    {
        if (dueDate == null) return SlaStatus.OnTrack;
        var remaining = (dueDate.Value - DateTime.UtcNow).TotalDays;
        if (remaining < 0) return SlaStatus.Overdue;
        if (remaining <= atRiskDays) return SlaStatus.AtRisk;
        return SlaStatus.OnTrack;
    }
}

// Lignes renvoyées au dashboard
public record TaskSlaItem(
    Guid Id,
    string Title,
    AcpTaskStatus Status,
    DateTime? DueDate,
    string? AssignedTo,
    string? AssignedToName,
    Guid? ProjectId,
    string? ProjectName,
    Guid? StreamId,
    string? StreamName,
    string? TaskThreadUrl,
    string? StreamChannelUrl,
    SlaStatus SlaStatus,
    int DaysRemaining);

public record StreamSlaItem(
    Guid Id,
    string Name,
    DateTime? DueDate,
    Guid ProjectId,
    string ProjectName,
    string? BusinessLeadName,
    string? TechnicalLeadName,
    string? MessagingChannelUrl,
    SlaStatus SlaStatus,
    int DaysRemaining);