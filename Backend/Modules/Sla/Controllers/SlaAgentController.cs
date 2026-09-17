using System.Text.Json;
using Backend.Data;
using Backend.Modules.Sla.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Sla.Controllers;

[ApiController]
[Route("api/sla-agent")]
[Authorize]
public class SlaAgentController : ControllerBase
{
    private readonly SlaAgentService _agent;
    private readonly AppDbContext _db;
    private readonly ILogger<SlaAgentController> _logger;

    public SlaAgentController(SlaAgentService agent, AppDbContext db, ILogger<SlaAgentController> logger)
    {
        _agent = agent;
        _db = db;
        _logger = logger;
    }

    // GET /api/sla-agent/risk-analysis?projectId=...
    // US75 / US76 — analyse IA à la demande pour un projet (bouton "AI Risk Insights" côté PM)
    [HttpGet("risk-analysis")]
    [Authorize(Roles = "ProjectManager,HeadOfCDS")]
    public async Task<IActionResult> GetRiskAnalysis([FromQuery] Guid projectId)
    {
        var json = await _agent.AnalyzeProjectRisksAsync(projectId);
        if (json == null)
            return StatusCode(500, new { message = "AI risk analysis failed. Please try again." });

        try
        {
            return Ok(JsonSerializer.Deserialize<object>(json));
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "SLA agent returned invalid JSON for project {ProjectId}", projectId);
            return StatusCode(500, new { message = "AI returned an unexpected response. Please try again." });
        }
    }

    // POST /api/sla-agent/weekly-report/generate
    // US77 — déclenchement manuel (test / à la demande), en plus du job automatique hebdomadaire
    [HttpPost("weekly-report/generate")]
    [Authorize(Roles = "HeadOfCDS,SuperAdmin")]
    public async Task<IActionResult> GenerateWeeklyReport()
    {
        var report = await _agent.GenerateWeeklyReportAsync();
        return Ok(report);
    }

    // GET /api/sla-agent/weekly-report/latest
    // US77 — consultation du dernier rapport généré (card "Weekly AI Report" dans le SLA Dashboard)
    [HttpGet("weekly-report/latest")]
    [Authorize(Roles = "HeadOfCDS")]
    public async Task<IActionResult> GetLatestWeeklyReport()
    {
        var latest = await _db.SlaWeeklyReports
            .OrderByDescending(r => r.GeneratedAt)
            .FirstOrDefaultAsync();

        if (latest == null)
            return NotFound(new { message = "No report generated yet" });

        return Ok(latest);
    }
}