using System.Text.Encodings.Web;
using System.Text.Json;
using Backend.Data;
using Backend.Modules.AI.Services;
using Backend.Modules.Auth.Models;
using Backend.Modules.Notifications.Services;
using Backend.Modules.Sla.Models;
using Backend.Modules.Sla.Tools;
using Backend.Modules.Tasks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace Backend.Modules.Sla.Services;

public class SlaAgentService
{
    private readonly Kernel _kernel;
    private readonly AppDbContext _db;
    private readonly SlaCheckerService _slaChecker;
    private readonly NotificationService _notificationService;
    private readonly KernelInvocationHelper _invocationHelper;
    private readonly ILogger<SlaAgentService> _logger;

    // camelCase (lu par le frontend) + accents non échappés (lu par le LLM)
    public static readonly JsonSerializerOptions DataJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public SlaAgentService(
        Kernel kernel,
        AppDbContext db,
        SlaCheckerService slaChecker,
        NotificationService notificationService,
        SlaAgentTools tools,
        KernelInvocationHelper invocationHelper,
        ILogger<SlaAgentService> logger)
    {
        _kernel = kernel;
        _db = db;
        _slaChecker = slaChecker;
        _notificationService = notificationService;
        _invocationHelper = invocationHelper;
        _logger = logger;

        _kernel.Plugins.AddFromObject(tools, "SlaAgentTools");
    }

    // ─────────────────────────────────────────────────────────────
    // US75 / US76 — Analyse à la demande pour un projet (Chef de Projet) — INCHANGÉ
    // ─────────────────────────────────────────────────────────────
    public async Task<string?> AnalyzeProjectRisksAsync(Guid projectId)
    {
        var prompt = $$"""
        You are an expert delivery risk analyst for an IT consulting portal.

        INSTRUCTIONS:
        1. Call get_project_risk_context("{{projectId}}") to get the open tasks for this project,
           each with its SLA status (OnTrack/AtRisk/Overdue, computed by fixed due-date rules)
           and its assignee's total open-task workload across all projects.
        2. Identify tasks that are a REAL delivery risk — this includes tasks already Overdue/AtRisk,
           AND tasks currently OnTrack but likely to slip because their assignee is overloaded
           (workload > 5 open tasks) or the due date is close relative to that workload.
        3. For each risky task, give a short reason (max 1 sentence).
        4. Write 2-4 concrete, actionable recommendations to reduce delays on this specific project
           (e.g. reassign specific work, prioritize specific tasks, flag a bottleneck consultant).

        Respond ONLY with this exact JSON structure, no markdown, no explanation:
        {
          "atRiskTasks": [ { "taskId": "guid", "title": "...", "reason": "..." } ],
          "recommendations": [ "..." ]
        }
        """;

        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        return await _invocationHelper.InvokeAsync(_kernel, prompt, settings, configPrefix: "Sla");
    }

    // ─────────────────────────────────────────────────────────────
    // US77 — Rapport hebdomadaire pour le Responsable CDS
    //   1. le backend calcule toutes les données (fiables)
    //   2. l'IA écrit uniquement l'analyse à partir de ces données
    // isManual = bouton "Générer maintenant" → pas de notification
    // ─────────────────────────────────────────────────────────────
    public async Task<SlaWeeklyReport> GenerateWeeklyReportAsync(bool isManual = false)
    {
        var now = DateTime.UtcNow;
        var data = await BuildWeeklyReportDataAsync(now);
        var dataJson = JsonSerializer.Serialize(data, DataJsonOptions);

        var prompt = $$"""
        You are writing the ANALYSIS part of a weekly SLA report for the Head of Delivery (Head of CDS)
        of an IT consulting company. The tables and numbers below are ALREADY displayed to the reader:
        do NOT repeat the tables and do NOT invent any number, project, task or person.

        DATA (JSON):
        - kpis: current counts. previous: last week's counts (null = first report) → comment the trend.
        - compliance: tasks completed in the last 7 days that had a due date, how many on time,
          onTimeRatePercent (null = no data yet).
        - projects: health of each active project (Red/Orange/Green) with its worst delay in days.
        - bottlenecks: people with the most open and overdue tasks.
        - newOverdue: tasks that became overdue this week. longestOverdue: oldest overdue tasks.

        {{dataJson}}

        Write markdown (max 300 words) with exactly this structure:
        ## Executive Summary
        2-3 sentences on overall delivery health and the trend versus last week.

        ## Key Risks
        3-5 bullet points naming specific projects, tasks or people from the data.

        ## Recommended Actions
        2-4 concrete bullet points for the Head of CDS (who to talk to, what to reassign, what to prioritize).

        Be direct and specific.
        """;

        var content = await _invocationHelper.InvokeAsync(_kernel, prompt, configPrefix: "Sla")
                      ?? "*AI analysis failed this week — the figures and tables above are still accurate. Please retry manually.*";

        var report = new SlaWeeklyReport
        {
            Content = content,
            OverdueTasksCount = data.Kpis.OverdueTasks,
            AtRiskTasksCount = data.Kpis.AtRiskTasks,
            OverdueStreamsCount = data.Kpis.OverdueStreams,
            AtRiskStreamsCount = data.Kpis.AtRiskStreams,
            GeneratedAt = now,
            WeekStart = SlaWeeklyReport.WeekStartOf(now),
            IsManual = isManual,
            DataJson = dataJson
        };

        _db.SlaWeeklyReports.Add(report);
        await _db.SaveChangesAsync();

        if (!isManual)
            await NotifyHeadsOfCdsAsync(data);

        _logger.LogInformation("Weekly SLA report generated: {Id} (manual: {Manual})", report.Id, isManual);
        return report;
    }

    // ───────────────────────── Calcul des données ─────────────────────────

    private async Task<WeeklyReportData> BuildWeeklyReportDataAsync(DateTime now)
    {
        var weekAgo = now.AddDays(-7);
        var currentWeek = SlaWeeklyReport.WeekStartOf(now);

        var tasks = await _slaChecker.GetOverdueAndAtRiskTasksAsync();
        var streams = await _slaChecker.GetOverdueAndAtRiskStreamsAsync();

        // 1. Chiffres clés + semaine précédente
        var kpis = new WeeklyKpis(
            tasks.Count(t => t.SlaStatus == SlaStatus.Overdue),
            tasks.Count(t => t.SlaStatus == SlaStatus.AtRisk),
            streams.Count(s => s.SlaStatus == SlaStatus.Overdue),
            streams.Count(s => s.SlaStatus == SlaStatus.AtRisk));

        var prev = await _db.SlaWeeklyReports
            .AsNoTracking()
            .Where(r => r.WeekStart < currentWeek)
            .OrderByDescending(r => r.GeneratedAt)
            .Select(r => new { r.OverdueTasksCount, r.AtRiskTasksCount, r.OverdueStreamsCount, r.AtRiskStreamsCount })
            .FirstOrDefaultAsync();

        var previous = prev == null ? null
            : new WeeklyKpis(prev.OverdueTasksCount, prev.AtRiskTasksCount, prev.OverdueStreamsCount, prev.AtRiskStreamsCount);

        // 2. Respect des SLA : tâches terminées ces 7 derniers jours (UpdatedAt = date de passage à Done)
        var completed = await _db.AcpTasks
            .AsNoTracking()
            .Where(t => t.Status == AcpTaskStatus.Done
                     && t.DueDate != null
                     && t.UpdatedAt != null
                     && t.UpdatedAt >= weekAgo)
            .Select(t => new { t.DueDate, t.UpdatedAt })
            .ToListAsync();

        var onTime = completed.Count(t => t.UpdatedAt <= t.DueDate);
        var compliance = new SlaComplianceStats(
            completed.Count,
            onTime,
            completed.Count > 0 ? (int)Math.Round(100.0 * onTime / completed.Count) : null);

        // 3. Santé par projet actif (= qui a des tâches ouvertes)
        var openByProject = await _db.AcpTasks
            .AsNoTracking()
            .Where(t => t.Status != AcpTaskStatus.Done && t.ProjectId != null)
            .GroupBy(t => t.ProjectId!.Value)
            .Select(g => new { ProjectId = g.Key, Open = g.Count() })
            .ToListAsync();

        var projectIds = openByProject.Select(p => p.ProjectId).ToList();
        var projectNames = (await _db.Projects
                .AsNoTracking()
                .Where(p => projectIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name })
                .ToListAsync())
            .ToDictionary(p => p.Id, p => p.Name);

        var projects = openByProject
            .Select(p =>
            {
                var overdue = tasks.Where(t => t.ProjectId == p.ProjectId && t.SlaStatus == SlaStatus.Overdue).ToList();
                var atRisk = tasks.Count(t => t.ProjectId == p.ProjectId && t.SlaStatus == SlaStatus.AtRisk);
                var projectStreams = streams.Where(s => s.ProjectId == p.ProjectId).ToList();
                var overdueStreams = projectStreams.Count(s => s.SlaStatus == SlaStatus.Overdue);

                var health = overdue.Any() || overdueStreams > 0 ? "Red"
                           : atRisk > 0 || projectStreams.Any() ? "Orange"
                           : "Green";

                return new ProjectHealthRow(
                    p.ProjectId,
                    projectNames.TryGetValue(p.ProjectId, out var name) ? name : "—",
                    p.Open,
                    overdue.Count,
                    atRisk,
                    overdueStreams,
                    overdue.Any() ? overdue.Max(t => -t.DaysRemaining) : 0,
                    health);
            })
            .OrderBy(p => p.Health == "Red" ? 0 : p.Health == "Orange" ? 1 : 2)
            .ThenByDescending(p => p.OverdueTasks)
            .ToList();

        // 4. Goulots d'étranglement : top 5 personnes (retards puis charge)
        var openByAssignee = await _db.AcpTasks
            .AsNoTracking()
            .Where(t => t.Status != AcpTaskStatus.Done && t.AssignedTo != null)
            .GroupBy(t => t.AssignedTo!)
            .Select(g => new { AssignedTo = g.Key, Open = g.Count() })
            .ToListAsync();

        var overdueByAssignee = tasks
            .Where(t => t.SlaStatus == SlaStatus.Overdue && t.AssignedTo != null)
            .GroupBy(t => t.AssignedTo!)
            .ToDictionary(g => g.Key, g => g.Count());

        var keycloakIds = openByAssignee.Select(a => a.AssignedTo).ToList();
        var userNames = (await _db.Users
                .AsNoTracking()
                .Where(u => u.KeycloakId != null && keycloakIds.Contains(u.KeycloakId))
                .Select(u => new { u.KeycloakId, u.FullName })
                .ToListAsync())
            .GroupBy(u => u.KeycloakId!)
            .ToDictionary(g => g.Key, g => g.First().FullName);

        var bottlenecks = openByAssignee
            .Select(a => new BottleneckRow(
                userNames.TryGetValue(a.AssignedTo, out var n) ? n : "—",
                a.Open,
                overdueByAssignee.TryGetValue(a.AssignedTo, out var o) ? o : 0))
            .OrderByDescending(b => b.OverdueTasks)
            .ThenByDescending(b => b.OpenTasks)
            .Take(5)
            .ToList();

        // 5. Retards : nouveaux cette semaine + les plus anciens
        OverdueTaskRow ToRow(TaskSlaItem t) => new(
            t.Title, t.ProjectName, t.StreamName, t.AssignedToName, t.DueDate!.Value, -t.DaysRemaining);

        var overdueTasks = tasks.Where(t => t.SlaStatus == SlaStatus.Overdue).ToList();

        var newOverdue = overdueTasks
            .Where(t => t.DueDate >= weekAgo)
            .OrderBy(t => t.DueDate)
            .Take(20)
            .Select(ToRow)
            .ToList();

        var longestOverdue = overdueTasks
            .Where(t => t.DueDate < weekAgo)
            .OrderBy(t => t.DueDate)
            .Take(5)
            .Select(ToRow)
            .ToList();

        return new WeeklyReportData(kpis, previous, compliance, projects, bottlenecks, newOverdue, longestOverdue);
    }

    private async Task NotifyHeadsOfCdsAsync(WeeklyReportData data)
    {
        var trend = data.Previous == null ? ""
            : $" ({data.Kpis.OverdueTasks - data.Previous.OverdueTasks:+0;-0;=} vs last week)";

        var rate = data.Compliance.OnTimeRatePercent.HasValue
            ? $", {data.Compliance.OnTimeRatePercent}% on time"
            : "";

        var message = $"Weekly SLA report ready — {data.Kpis.OverdueTasks} overdue tasks{trend}{rate}";

        var heads = await _db.Users
            .Where(u => u.Role == GlobalRole.HeadOfCDS)
            .ToListAsync();

        foreach (var user in heads)
            await _notificationService.SendAsync(user.KeycloakId, message, null);
    }
}