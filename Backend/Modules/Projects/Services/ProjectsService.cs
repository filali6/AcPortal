using Backend.Data;
using Backend.Modules.Projects.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Projects.Services;

public class ProjectsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ProjectsService> _logger;

    public ProjectsService(AppDbContext db, ILogger<ProjectsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Créer un projet (SuperAdmin)
    public async Task<Project> CreateAsync(string name, string description, Guid portfolioDirectorId)
    {
        var project = new Project
        {
            Name = name,
            Description = description,
            PortfolioDirectorId = portfolioDirectorId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Projet créé : {Name}", name);
        return project;
    }

    // Liste tous les projets
    public async Task<List<Project>> GetAllAsync()
    {
        return await _db.Projects
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    // Détail d'un projet
    public async Task<Project?> GetByIdAsync(Guid id)
    {
        return await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    // Affecter un projet à un Director
    public async Task<Project?> AssignDirectorAsync(Guid projectId, Guid directorId)
    {
        var project = await _db.Projects.FindAsync(projectId);
        if (project == null) return null;

        // Vérifier que le Director existe
        var director = await _db.Users.FindAsync(directorId);
        if (director == null) return null;

        project.PortfolioDirectorId = directorId;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Projet {Id} affecté au Director {DirectorId}", projectId, directorId);
        return project;
    }
}