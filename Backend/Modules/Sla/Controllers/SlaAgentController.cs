using System.Text.Json;
using Backend.Data;
using Backend.Modules.Sla.Models;
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

    // GET /api/sla-agent/risk-analysis?projectId=...  — INCHANGÉ (écran du PM)
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

    // POST /api/sla-agent/weekly-report/generate — bouton "Générer maintenant" (pas de notification)
    [HttpPost("weekly-report/generate")]
    [Authorize(Roles = "HeadOfCDS,SuperAdmin")]
    public async Task<IActionResult> GenerateWeeklyReport()
    {
        var report = await _agent.GenerateWeeklyReportAsync(isManual: true);
        return Ok(ToDto(report));
    }

    // GET /api/sla-agent/weekly-report/latest — dernier rapport, complet
    [HttpGet("weekly-report/latest")]
    [Authorize(Roles = "HeadOfCDS,SuperAdmin")]
    public async Task<IActionResult> GetLatestWeeklyReport()
    {
        var latest = await _db.SlaWeeklyReports
            .AsNoTracking()
            .OrderByDescending(r => r.GeneratedAt)
            .FirstOrDefaultAsync();

        if (latest == null)
            return NotFound(new { message = "No report generated yet" });

        return Ok(ToDto(latest));
    }

    // GET /api/sla-agent/weekly-report/history — liste légère des 12 derniers rapports
    [HttpGet("weekly-report/history")]
    [Authorize(Roles = "HeadOfCDS,SuperAdmin")]
    public async Task<IActionResult> GetWeeklyReportHistory()
    {
        var reports = await _db.SlaWeeklyReports
            .AsNoTracking()
            .OrderByDescending(r => r.GeneratedAt)
            .Take(12)
            .Select(r => new
            {
                r.Id,
                r.GeneratedAt,
                r.WeekStart,
                r.IsManual,
                r.OverdueTasksCount,
                r.AtRiskTasksCount,
                r.OverdueStreamsCount,
                r.AtRiskStreamsCount
            })
            .ToListAsync();

        return Ok(reports);
    }

    // GET /api/sla-agent/weekly-report/{id} — un rapport précis (depuis l'historique)
    [HttpGet("weekly-report/{id:guid}")]
    [Authorize(Roles = "HeadOfCDS,SuperAdmin")]
    public async Task<IActionResult> GetWeeklyReport(Guid id)
    {
        var report = await _db.SlaWeeklyReports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (report == null) return NotFound(new { message = "Report not found" });
        return Ok(ToDto(report));
    }

    // Les anciens rapports (avant cette étape) n'ont pas de DataJson → data = null, seul le texte s'affiche
    private object ToDto(SlaWeeklyReport r) => new
    {
        r.Id,
        r.GeneratedAt,
        r.WeekStart,
        r.IsManual,
        r.Content,
        r.OverdueTasksCount,
        r.AtRiskTasksCount,
        r.OverdueStreamsCount,
        r.AtRiskStreamsCount,
        data = ParseData(r.DataJson)
    };

    private object? ParseData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid DataJson in weekly report");
            return null;
        }
    }
}