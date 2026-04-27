using Backend.Data;
using Backend.Modules.Tools.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Backend.Modules.Tools.Controllers;

[ApiController]
[Route("api/tools")]
public class ToolsController : ControllerBase
{
    private readonly ToolsService _toolsService;
    private readonly AppDbContext _db;

    public ToolsController(ToolsService toolsService, AppDbContext db)
    {
        _toolsService = toolsService;
        _db=db;

    }

    // GET api/tools
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll()
    {
        var tools = await _toolsService.GetAllToolsAsync();
        return Ok(tools);
    }

    // POST api/tools
    [HttpPost]
    [Authorize(Roles ="HeadOfCDS")]
    public async Task<IActionResult> CreateTool([FromBody] CreateToolRequest request)
    {
        var tool = await _toolsService.CreateToolAsync(
            request.Name,
            request.Description
        );

        return Ok(new
        {
            message = "Outil créé avec succès",
            data = tool
        });
    }

    // POST api/tools/{id}/roles
    [HttpPost("{id:guid}/roles")]
    [Authorize(Roles ="HeadOfCDS")]
    public async Task<IActionResult> CreateRole(Guid id, [FromBody] CreateRoleRequest request)
    {
        var role = await _toolsService.CreateRoleAsync(request.Name, id);
        if (role == null)
            return NotFound(new { message = "Outil introuvable" });

        return Ok(new
        {
            message = "Rôle créé avec succès",
            data = role
        });
    }

    // GET api/tools/{id}/roles
    [HttpGet("{id:guid}/roles")]
    [Authorize]
    public async Task<IActionResult> GetRoles(Guid id)
    {
        var roles = await _toolsService.GetRolesByToolAsync(id);
        return Ok(roles);
    }

    // POST api/tools/assign
    [HttpPost("assign")]
     
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest request)
    {
        var result = await _toolsService.AssignRoleAsync(
            request.ConsultantId,
            request.ToolId,
            request.ToolRoleId
        );

        if (result == null)
            return BadRequest(new { message = "Données invalides ou rôle déjà assigné" });

        return Ok(new
        {
            message = "Rôle assigné avec succès",
            data = result
        });
    }

    // GET api/tools/consultant/{id}
    [HttpGet("consultant/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetConsultantRoles(Guid id)
    {
        var roles = await _toolsService.GetConsultantRolesAsync(id);
        return Ok(roles);
    }

    // GET api/tools/access
    [HttpGet("access")]
    [Authorize]
    public async Task<IActionResult> CheckAccess(
        [FromQuery] Guid consultantId,
        [FromQuery] Guid toolId)
    {
        var hasAccess = await _toolsService.HasAccessAsync(consultantId, toolId);
        return Ok(new { hasAccess = hasAccess });
    }
    [HttpGet("my-roles")]
    [Authorize]
    public async Task<IActionResult> GetMyRoles()
    {
        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        if (user == null) return NotFound();

        var roles = await _toolsService.GetMyRolesGroupedAsync(user.Id);
        return Ok(roles);
    }
}

public class CreateToolRequest
{
    [Required(ErrorMessage = "Name est obligatoire")]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

public class CreateRoleRequest
{
    [Required(ErrorMessage = "Name est obligatoire")]
    public string Name { get; set; } = string.Empty;
}

public class AssignRoleRequest
{
    [Required]
    public Guid ConsultantId { get; set; }

    [Required]
    public Guid ToolId { get; set; }

    [Required]
    public Guid ToolRoleId { get; set; }
}