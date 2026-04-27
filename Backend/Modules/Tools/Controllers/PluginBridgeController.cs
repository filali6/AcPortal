using Backend.Data;
using Backend.Modules.Tools.Models;
using Backend.Modules.Tools.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Tools.Controllers;

[ApiController]
[Route("api/plugins")]
[Authorize]
public class PluginBridgeController : ControllerBase
{
    private readonly PluginRegistry _registry;
    private readonly AppDbContext _db;

    public PluginBridgeController(PluginRegistry registry, AppDbContext db)
    {
        _registry = registry;
        _db = db;
    }

   
    [HttpGet]
    public IActionResult GetAll()
    {
        var plugins = _registry.GetAll().Select(p => new
        {
            p.Id,
            p.Name,
            p.Description,
            p.Category,
            p.Icon,
            p.SsoEnabled,
            accessUrl = _registry.GetAdapter(p.Id)?.GetAccessUrl()
        });
        return Ok(plugins);
    }

   
    [HttpGet("{pluginId}")]
    public IActionResult GetById(string pluginId)
    {
        var plugin = _registry.GetById(pluginId);
        if (plugin == null) return NotFound();

        var adapter = _registry.GetAdapter(pluginId);
        return Ok(new
        {
            plugin.Id,
            plugin.Name,
            plugin.Description,
            plugin.Category,
            plugin.Icon,
            plugin.SsoEnabled,
            accessUrl = adapter?.GetAccessUrl()
        });
    }
 
    [HttpGet("user/my")]
    public async Task<IActionResult> GetMyPlugins()
    {
        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        if (user == null) return NotFound();

        var userPlugins = await _db.UserPlugins
            .Where(up => up.UserId == user.Id)
            .ToListAsync();

        var result = userPlugins.Select(up =>
        {
            var plugin = _registry.GetById(up.PluginId);
            if (plugin == null) return null;
            var adapter = _registry.GetAdapter(up.PluginId);
            return new
            {
                plugin.Id,
                plugin.Name,
                plugin.Description,
                plugin.Category,
                plugin.Icon,
                plugin.SsoEnabled,
                accessUrl = adapter?.GetAccessUrl(),
                addedAt = up.AddedAt
            };
        }).Where(p => p != null);

        return Ok(result);
    }

    
    [HttpPost("user/{pluginId}")]
    public async Task<IActionResult> AddPlugin(string pluginId)
    {
        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        if (user == null) return NotFound();

        var plugin = _registry.GetById(pluginId);
        if (plugin == null) return NotFound(new { message = "Plugin introuvable" });

        var exists = await _db.UserPlugins
            .AnyAsync(up => up.UserId == user.Id && up.PluginId == pluginId);
        if (exists) return BadRequest(new { message = "Plugin déjà ajouté" });

        _db.UserPlugins.Add(new UserPlugin
        {
            UserId = user.Id,
            PluginId = pluginId
        });
        await _db.SaveChangesAsync();

        return Ok(new { message = "Plugin ajouté avec succès" });
    }
 
    [HttpDelete("user/{pluginId}")]
    public async Task<IActionResult> RemovePlugin(string pluginId)
    {
        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        if (user == null) return NotFound();

        var userPlugin = await _db.UserPlugins
            .FirstOrDefaultAsync(up => up.UserId == user.Id && up.PluginId == pluginId);
        if (userPlugin == null) return NotFound();

        _db.UserPlugins.Remove(userPlugin);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Plugin retiré avec succès" });
    }
}