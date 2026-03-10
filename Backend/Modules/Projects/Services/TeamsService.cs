using Backend.Data;
using Backend.Modules.Projects.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Projects.Services;

public class TeamsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TeamsService> _logger;

    public TeamsService(AppDbContext db, ILogger<TeamsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Créer une équipe pour un projet
    // public async Task<Team?> CreateAsync(string name, Guid projectId, Guid chefEquipeId)
    // {
    //     // Vérifier que le projet existe
    //     var project = await _db.Projects.FindAsync(projectId);
    //     if (project == null) return null;

    //     // Vérifier qu'il n'y a pas déjà une équipe pour ce projet
    //     var exists = await _db.Teams.AnyAsync(t => t.ProjectId == projectId);
    //     if (exists) return null;

    //     var chefEquipe = await _db.Users.FindAsync(chefEquipeId);
    //     if (chefEquipe == null) return null;

    //     var team = new Team
    //     {
    //         Name = name,
    //         ProjectId = projectId,
    //         ChefEquipeId = chefEquipeId,
    //         CreatedAt = DateTime.UtcNow
    //     };

    //     _db.Teams.Add(team);
    //     await _db.SaveChangesAsync();

    //     _logger.LogInformation("Equipe créée : {Name}", name);
    //     return team;
    // }
    public async Task<Team?> CreateAsync(string name, Guid projectId, Guid chefEquipeId)
    {
        // Vérifier que le projet existe
        var project = await _db.Projects.FindAsync(projectId);
        _logger.LogInformation("Project found: {Found}", project != null);
        if (project == null) return null;

        // Vérifier qu'il n'y a pas déjà une équipe pour ce projet
        var exists = await _db.Teams.AnyAsync(t => t.ProjectId == projectId);
        _logger.LogInformation("Team exists: {Exists}", exists);
        if (exists) return null;

        // Vérifier que le chef d'équipe existe
        var chefEquipe = await _db.Users.FindAsync(chefEquipeId);
        _logger.LogInformation("ChefEquipe found: {Found}", chefEquipe != null);
        if (chefEquipe == null) return null;

        var team = new Team
        {
            Name = name,
            ProjectId = projectId,
            ChefEquipeId = chefEquipeId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Teams.Add(team);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Equipe créée : {Name}", name);
        return team;
    }
    // Détail d'une équipe
    public async Task<Team?> GetByIdAsync(Guid id)
    {
        return await _db.Teams.FirstOrDefaultAsync(t => t.Id == id);
    }

    // Ajouter un consultant à l'équipe
    // Ajouter plusieurs consultants en une seule fois
    public async Task<List<TeamMember>> AddMembersAsync(Guid teamId, List<Guid> consultantIds)
    {
        var team = await _db.Teams.FindAsync(teamId);
        if (team == null) return new List<TeamMember>();

        var members = new List<TeamMember>();

        foreach (var consultantId in consultantIds)
        {
            // Vérifier que le consultant existe
            var consultant = await _db.Users.FindAsync(consultantId);
            if (consultant == null) continue;

            // Vérifier qu'il n'est pas déjà membre
            var exists = await _db.TeamMembers.AnyAsync(tm =>
                tm.TeamId == teamId && tm.ConsultantId == consultantId);
            if (exists) continue;

            members.Add(new TeamMember
            {
                TeamId = teamId,
                ConsultantId = consultantId,
                JoinedAt = DateTime.UtcNow
            });
        }

        _db.TeamMembers.AddRange(members);
        await _db.SaveChangesAsync();

        return members;
    }

    // Liste des membres d'une équipe
    public async Task<List<TeamMember>> GetMembersAsync(Guid teamId)
    {
        return await _db.TeamMembers
            .Where(tm => tm.TeamId == teamId)
            .ToListAsync();
    }
}