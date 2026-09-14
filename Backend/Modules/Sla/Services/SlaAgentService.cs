using System.Text.Encodings.Web;
using System.Text.Json;
using Backend.Data;
using Backend.Modules.AI.Services;
using Backend.Modules.Auth.Models;
using Backend.Modules.Notifications.Services;
using Backend.Modules.Sla.Models;
using Backend.Modules.Sla.Tools;
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

    // Désactive l'échappement \uXXXX des caractères accentués (é, à, ê...) pour tout JSON
    // destiné à être lu par le LLM dans un prompt — sinon il recopie les séquences échappées telles quelles.
    private static readonly JsonSerializerOptions PromptJsonOptions = new()
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

        // Enregistre le tool dans le kernel — même pattern que FsdPlanningService/PlanningTools
        _kernel.Plugins.AddFromObject(tools, "SlaAgentTools");
    }

    // ─────────────────────────────────────────────────────────────
    // US75 / US76 — Analyse à la demande pour un projet (Chef de Projet)
    // Vrai agent à tool : le LLM appelle lui-même get_project_risk_context(),
    // au lieu de recevoir un contexte pré-calculé en dur dans le prompt.
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
    // US77 — Rapport hebdomadaire global (tous projets), pour le Responsable CDS.
    // Pas besoin de tool ici : les données sont déjà entièrement agrégées par
    // SlaCheckerService (pas d'exploration à faire), un appel direct suffit.
    // ─────────────────────────────────────────────────────────────
    public async Task<SlaWeeklyReport> GenerateWeeklyReportAsync()
    {
        var overdueTasks = await _slaChecker.GetOverdueAndAtRiskTasksAsync();
        var overdueStreams = await _slaChecker.GetOverdueAndAtRiskStreamsAsync();

        // Les items sont des anonymous types avec SlaStatus (enum, sérialisé en int : OnTrack=0, AtRisk=1, Overdue=2)
        var tasksJson = JsonSerializer.Serialize(overdueTasks);
        var streamsJson = JsonSerializer.Serialize(overdueStreams);
        using var tasksDoc = JsonDocument.Parse(tasksJson);
        using var streamsDoc = JsonDocument.Parse(streamsJson);

        static int CountByStatus(JsonDocument doc, int statusValue) =>
            doc.RootElement.EnumerateArray()
                .Count(e => e.GetProperty("SlaStatus").GetInt32() == statusValue);

        var overdueTasksN = CountByStatus(tasksDoc, 2);   // Overdue
        var atRiskTasksN = CountByStatus(tasksDoc, 1);    // AtRisk
        var overdueStreamsN = CountByStatus(streamsDoc, 2);
        var atRiskStreamsN = CountByStatus(streamsDoc, 1);

        var prompt = $$"""
        You are writing a concise weekly SLA risk report for a Head of Delivery (Head of CDS)
        overseeing multiple IT consulting projects.

        SUMMARY COUNTS:
        - Overdue tasks: {{overdueTasksN}}
        - At-risk tasks (due within 2 days): {{atRiskTasksN}}
        - Overdue streams: {{overdueStreamsN}}
        - At-risk streams: {{atRiskStreamsN}}

        TOP OVERDUE/AT-RISK TASKS (JSON, may be truncated):
        {{JsonSerializer.Serialize(overdueTasks.Take(30), PromptJsonOptions)}}

        TOP OVERDUE/AT-RISK STREAMS (JSON):
        {{JsonSerializer.Serialize(overdueStreams.Take(20), PromptJsonOptions)}}

        Write a short markdown report (max 400 words) with this exact structure:
        ## Weekly SLA Report
        A 2-sentence executive summary of the overall delivery health.

        ## Key Risks
        3-5 bullet points on the most concerning items (name specific projects/tasks when relevant).

        ## Recommended Actions
        2-4 bullet points of concrete next steps for the Head of CDS.

        Be direct and specific. No invented data — use only what's provided above.
        """;

        var content = await _invocationHelper.InvokeAsync(_kernel, prompt, configPrefix: "Sla")
                      ?? "*Report generation failed this week — please retry manually.*";

        var report = new SlaWeeklyReport
        {
            Content = content,
            OverdueTasksCount = overdueTasksN,
            AtRiskTasksCount = atRiskTasksN,
            OverdueStreamsCount = overdueStreamsN,
            AtRiskStreamsCount = atRiskStreamsN
        };

        _db.SlaWeeklyReports.Add(report);
        await _db.SaveChangesAsync();

        // Notifie tous les Responsables CDS
        var headOfCdsUsers = await _db.Users
            .Where(u => u.Role == GlobalRole.HeadOfCDS)
            .ToListAsync();

        foreach (var user in headOfCdsUsers)
        {
            await _notificationService.SendAsync(
                user.KeycloakId,
                $"Weekly SLA report ready — {overdueTasksN} overdue tasks, {overdueStreamsN} overdue streams",
                null
            );
        }

        _logger.LogInformation("Weekly SLA report generated: {Id}", report.Id);
        return report;
    }
}