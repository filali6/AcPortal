using Backend.Data;
using Backend.Modules.Sla.Models;
using Backend.Modules.Tasks.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Sla.Services;

public class SlaCheckerService
{
    private readonly AppDbContext _db;
    private readonly ILogger<SlaCheckerService> _logger;

    public SlaCheckerService(AppDbContext db, ILogger<SlaCheckerService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Calcule le statut SLA d'une tâche
    public SlaStatus GetTaskSlaStatus(AcpTask task)
    {
        if (task.DueDate == null) return SlaStatus.OnTrack;

        var now = DateTime.UtcNow;
        var dueDate = task.DueDate.Value;
        var daysRemaining = (dueDate - now).TotalDays;

        if (daysRemaining < 0)
            return SlaStatus.Overdue;

        if (daysRemaining <= 2)
            return SlaStatus.AtRisk;

        return SlaStatus.OnTrack;
    }

    // Calcule le statut SLA d'un stream
    public SlaStatus GetStreamSlaStatus(DateTime? dueDate)
    {
        if (dueDate == null) return SlaStatus.OnTrack;

        var now = DateTime.UtcNow;
        var remaining = (dueDate.Value - now).TotalDays;

        if (remaining < 0) return SlaStatus.Overdue;
        if (remaining <= 3) return SlaStatus.AtRisk;
        return SlaStatus.OnTrack;
    }

    // Applique les règles SLA à toutes les tâches sans DueDate
    public async Task ApplySlaRulesToTasksAsync()
    {
        var rules = await _db.SlaRules
            .Where(r => r.Type == SlaRuleType.Task)
            .ToListAsync();

        if (!rules.Any()) return;

        // Prend la première règle par défaut (la plus courte)
        var defaultRule = rules.OrderBy(r => r.SlaDays).First();

        var tasksWithoutDueDate = await _db.AcpTasks
            .Where(t => t.DueDate == null && t.Status != AcpTaskStatus.Done)
            .ToListAsync();

        foreach (var task in tasksWithoutDueDate)
        {
            task.DueDate = task.CreatedAt.AddDays(defaultRule.SlaDays);
            task.SlaRuleId = defaultRule.Id;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("SLA applied to {Count} tasks", tasksWithoutDueDate.Count);
    }

    // Retourne toutes les tâches en retard ou à risque
    public async Task<List<object>> GetOverdueAndAtRiskTasksAsync()
    {
        var now = DateTime.UtcNow;

        var tasks = await _db.AcpTasks
            .Where(t =>
                t.DueDate != null &&
                t.Status != AcpTaskStatus.Done)
            .Include(t => t.Comments)
            .ToListAsync();

        return tasks
            .Select(t => new
            {
                t.Id,
                t.Title,
                t.Status,
                t.DueDate,
                t.AssignedTo,
                t.ProjectId,
                t.StreamId,
                SlaStatus = GetTaskSlaStatus(t),
                DaysRemaining = t.DueDate.HasValue
                    ? (int)Math.Round((t.DueDate.Value - now).TotalDays)
                    : (int?)null
            })
            .Where(t => t.SlaStatus != SlaStatus.OnTrack)
            .OrderBy(t => t.DaysRemaining)
            .Cast<object>()
            .ToList();
    }

    // Retourne tous les streams en retard ou à risque
    public async Task<List<object>> GetOverdueAndAtRiskStreamsAsync()
    {
        var now = DateTime.UtcNow;

        var streams = await _db.Streams
            .Include(s => s.Project)
            .Where(s => s.DueDate != null)
            .ToListAsync();

        return streams
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.DueDate,
                ProjectName = s.Project.Name,
                SlaStatus = GetStreamSlaStatus(s.DueDate),
                DaysRemaining = s.DueDate.HasValue
                    ? (int)Math.Round((s.DueDate.Value - now).TotalDays)
                    : (int?)null
            })
            .Where(s => s.SlaStatus != SlaStatus.OnTrack)
            .OrderBy(s => s.DaysRemaining)
            .Cast<object>()
            .ToList();
    }
}