using Backend.Data;
using Backend.Modules.Dashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Dashboard.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly BriefingService _briefingService;
    private readonly AppDbContext _db;

    public DashboardController(BriefingService briefingService, AppDbContext db)
    {
        _briefingService = briefingService;
        _db = db;
    }
 
    [HttpGet("briefing")]
    public async Task<IActionResult> GetBriefing()
    {
        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        if (user == null) return NotFound();

        var role = user.Role.ToString();

        var briefing = await _briefingService.GenerateBriefingAsync(user.Id, keycloakId, user.FullName, role);

        return Ok(new { briefing });
    }
}