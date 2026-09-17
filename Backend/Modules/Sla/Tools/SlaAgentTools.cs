using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using Backend.Data;
using Backend.Modules.Sla.Services;
using Backend.Modules.Tasks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace Backend.Modules.Sla.Tools;

public class SlaAgentTools
{
    private readonly AppDbContext _db;
    private readonly SlaCheckerService _slaChecker;

    // Désactive l'échappement \uXXXX des caractères accentués — ce JSON est lu par le LLM.
    private static readonly JsonSerializerOptions PromptJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public SlaAgentTools(AppDbContext db, SlaCheckerService slaChecker)
    {
        _db = db;
        _slaChecker = slaChecker;
    }

    [KernelFunction("get_project_risk_context")]
    [Description("Returns all open (non-Done) tasks for a project with their SLA status " +
                 "(OnTrack/AtRisk/Overdue, computed by fixed due-date rules) and each assignee's " +
                 "total open-task workload across all projects. Call this first before analyzing risks.")]
    public async Task<string> GetProjectRiskContextAsync(
        [Description("The project ID (GUID) to analyze")] string projectId)
    {
        if (!Guid.TryParse(projectId, out var pid))
            return JsonSerializer.Serialize(new { error = "Invalid projectId" }, PromptJsonOptions);

        var now = DateTime.UtcNow;

        var tasks = await _db.AcpTasks
            .Where(t => t.ProjectId == pid && t.Status != AcpTaskStatus.Done)
            .ToListAsync();

        if (!tasks.Any())
            return JsonSerializer.Serialize(new { tasks = Array.Empty<object>() }, PromptJsonOptions);

        // AssignedTo est un string (pas un Guid?) sur AcpTask
        var assigneeIds = tasks
            .Where(t => !string.IsNullOrEmpty(t.AssignedTo))
            .Select(t => t.AssignedTo!)
            .Distinct()
            .ToList();

        var workloads = await _db.AcpTasks
            .Where(t => t.Status != AcpTaskStatus.Done
                     && t.AssignedTo != null
                     && assigneeIds.Contains(t.AssignedTo))
            .GroupBy(t => t.AssignedTo)
            .Select(g => new { AssignedTo = g.Key, OpenTaskCount = g.Count() })
            .ToListAsync();

        var result = tasks.Select(t => new
        {
            t.Id,
            t.Title,
            Status = t.Status.ToString(),
            t.DueDate,
            DaysRemaining = t.DueDate.HasValue
                ? (int)Math.Round((t.DueDate.Value - now).TotalDays)
                : (int?)null,
            SlaStatus = _slaChecker.GetTaskSlaStatus(t).ToString(),
            AssigneeWorkload = !string.IsNullOrEmpty(t.AssignedTo)
                ? workloads.FirstOrDefault(w => w.AssignedTo == t.AssignedTo)?.OpenTaskCount ?? 1
                : (int?)null
        });

        return JsonSerializer.Serialize(new { tasks = result }, PromptJsonOptions);
    }
}