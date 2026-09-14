using Backend.Data;
using Backend.Modules.Sla.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Sla.Controllers;

[ApiController]
[Route("api/sla-rules")]
public class SlaRulesController : ControllerBase
{
    private readonly AppDbContext _db;

    public SlaRulesController(AppDbContext db)
    {
        _db = db;
    }

    // US59 — GET toutes les règles
    [HttpGet]
    [Authorize(Roles = "SuperAdmin,HeadOfCDS")]
    public async Task<IActionResult> GetAll()
    {
        var rules = await _db.SlaRules
            .OrderBy(r => r.SlaDays)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Description,
                r.Type,
                r.SlaDays,
                r.CreatedAt
            })
            .ToListAsync();

        return Ok(rules);
    }

    // US59 — POST créer une règle
    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Create([FromBody] CreateSlaRuleRequest request)
    {
        if (string.IsNullOrEmpty(request.Name))
            return BadRequest(new { message = "Name is required" });

        if (request.SlaDays <= 0)
            return BadRequest(new { message = "SlaDays must be greater than 0" });

        var rule = new SlaRule
        {
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            SlaDays = request.SlaDays
        };

        _db.SlaRules.Add(rule);
        await _db.SaveChangesAsync();

        return Ok(new { message = "SLA rule created", data = rule });
    }

    // US60 — PATCH modifier une règle
    [HttpPatch("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSlaRuleRequest request)
    {
        var rule = await _db.SlaRules.FindAsync(id);
        if (rule == null)
            return NotFound(new { message = "SLA rule not found" });

        if (!string.IsNullOrEmpty(request.Name))
            rule.Name = request.Name;

        if (request.Description != null)
            rule.Description = request.Description;

        if (request.SlaDays.HasValue && request.SlaDays.Value > 0)
            rule.SlaDays = request.SlaDays.Value;

        if (request.Type.HasValue)
            rule.Type = request.Type.Value;

        await _db.SaveChangesAsync();

        return Ok(new { message = "SLA rule updated", data = rule });
    }

    // US61 — DELETE supprimer une règle
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var rule = await _db.SlaRules.FindAsync(id);
        if (rule == null)
            return NotFound(new { message = "SLA rule not found" });

        _db.SlaRules.Remove(rule);
        await _db.SaveChangesAsync();

        return Ok(new { message = "SLA rule deleted" });
    }
}

// Request models
public class CreateSlaRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SlaRuleType Type { get; set; } = SlaRuleType.Task;
    public int SlaDays { get; set; }
}

public class UpdateSlaRuleRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? SlaDays { get; set; }
    public SlaRuleType? Type { get; set; }
}