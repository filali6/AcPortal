using Backend.Data;
using Backend.Modules.Tools.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Tools.Services;

public class ToolsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ToolsService> _logger;

    public ToolsService(AppDbContext db, ILogger<ToolsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Créer un outil ACP
    public async Task<AcpTool> CreateToolAsync(string name, string description)
    {
        var tool = new AcpTool
        {
            Name = name,
            Description = description
        };

        _db.AcpTools.Add(tool);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Outil créé : {Name}", name);
        return tool;
    }

    // Liste tous les outils
    public async Task<List<AcpTool>> GetAllToolsAsync()
    {
        return await _db.AcpTools.ToListAsync();
    }

    // Créer un rôle pour un outil
    public async Task<ToolRole?> CreateRoleAsync(string name, Guid toolId)
    {
        // Vérifier que l'outil existe
        var tool = await _db.AcpTools.FindAsync(toolId);
        if (tool == null) return null;

        var role = new ToolRole
        {
            Name = name,
            ToolId = toolId
        };

        _db.ToolRoles.Add(role);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Rôle {Name} créé pour l'outil {ToolId}", name, toolId);
        return role;
    }

    // Liste les rôles d'un outil
    public async Task<List<ToolRole>> GetRolesByToolAsync(Guid toolId)
    {
        return await _db.ToolRoles
            .Where(r => r.ToolId == toolId)
            .ToListAsync();
    }

    // Assigner un rôle outil à un consultant
    public async Task<ConsultantToolRole?> AssignRoleAsync(
        Guid consultantId, Guid toolId, Guid toolRoleId)
    {
        // Vérifier que le consultant existe
        var consultant = await _db.Users.FindAsync(consultantId);
        if (consultant == null) return null;

        // Vérifier que l'outil existe
        var tool = await _db.AcpTools.FindAsync(toolId);
        if (tool == null) return null;

        // Vérifier que le rôle existe
        var role = await _db.ToolRoles.FindAsync(toolRoleId);
        if (role == null) return null;

        // Vérifier que ce rôle n'est pas déjà assigné
        var exists = await _db.ConsultantToolRoles.AnyAsync(c =>
            c.ConsultantId == consultantId &&
            c.ToolId == toolId &&
            c.ToolRoleId == toolRoleId);
        if (exists) return null;

        var consultantRole = new ConsultantToolRole
        {
            ConsultantId = consultantId,
            ToolId = toolId,
            ToolRoleId = toolRoleId,
            AssignedAt = DateTime.UtcNow
        };

        _db.ConsultantToolRoles.Add(consultantRole);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Rôle {RoleId} assigné au consultant {ConsultantId} pour l'outil {ToolId}",
            toolRoleId, consultantId, toolId);
        return consultantRole;
    }

    // Liste les rôles d'un consultant
    public async Task<List<ConsultantToolRole>> GetConsultantRolesAsync(Guid consultantId)
    {
        return await _db.ConsultantToolRoles
            .Where(c => c.ConsultantId == consultantId)
            .ToListAsync();
    }
    // Retourne les rôles du consultant connecté groupés par outil
    public async Task<List<object>> GetMyRolesGroupedAsync(Guid consultantId)
    {
        var allTools = await _db.AcpTools.ToListAsync();

        var myRoles = await _db.ConsultantToolRoles
            .Where(c => c.ConsultantId == consultantId)
            .ToListAsync();

        var result = new List<object>();

        foreach (var tool in allTools)
        {
            var rolesForTool = myRoles.Where(r => r.ToolId == tool.Id).ToList();

            var roleNames = new List<string>();
            foreach (var r in rolesForTool)
            {
                var role = await _db.ToolRoles.FindAsync(r.ToolRoleId);
                if (role != null) roleNames.Add(role.Name);
            }

            result.Add(new
            {
                toolId = tool.Id,
                toolName = tool.Name,
                roles = roleNames
            });
        }

        return result;
    }

    // Vérifier si un consultant a accès à un outil
    public async Task<bool> HasAccessAsync(Guid consultantId, Guid toolId)
    {
        return await _db.ConsultantToolRoles.AnyAsync(c =>
            c.ConsultantId == consultantId &&
            c.ToolId == toolId);
    }
}