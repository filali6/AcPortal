using Backend.Data;
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
                totalOverdueTasks = overdueTasks.Count(t =>
                    ((dynamic)t).SlaStatus.ToString() == "Overdue"),
                totalAtRiskTasks = overdueTasks.Count(t =>
                    ((dynamic)t).SlaStatus.ToString() == "AtRisk"),
                totalOverdueStreams = overdueStreams.Count(s =>
                    ((dynamic)s).SlaStatus.ToString() == "Overdue"),
                totalAtRiskStreams = overdueStreams.Count(s =>
                    ((dynamic)s).SlaStatus.ToString() == "AtRisk")
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

    // Appliquer règles SLA à toutes les tâches (SuperAdmin)
    [HttpPost("apply-rules")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> ApplyRules()
    {
        await _slaChecker.ApplySlaRulesToTasksAsync();
        return Ok(new { message = "SLA rules applied to all tasks" });
    }
}

public class SetDueDateRequest
{
    public DateTime? DueDate { get; set; }
}