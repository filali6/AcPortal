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

 
    public async Task<Project> CreateAsync(string name, string description, Guid portfolioId)
    {
        var project = new Project
        {
            Name = name,
            Description = description,
            PortfolioId = portfolioId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Projet créé : {Name}", name);
        return project;
    }

    
    public async Task<List<Project>> GetAllAsync()
    {
        return await _db.Projects
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

     
    public async Task<Project?> GetByIdAsync(Guid id)
    {
        return await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    
    
    public async Task<List<Project>> GetMyProjectsAsync(Guid directorId)
    {
        return await _db.Projects
            .Include(p => p.Portfolio)
            .Where(p => p.Portfolio != null && p.Portfolio.PortfolioDirectorId == directorId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }
    public async Task<Project> CreateAsync(string name, string description, Guid portfolioId, DateTime? targetDate = null)
    {
        var project = new Project
        {
            Name = name,
            Description = description,
            PortfolioId = portfolioId,
            TargetDate = targetDate.HasValue
            ? DateTime.SpecifyKind(targetDate.Value, DateTimeKind.Utc)
            : null,
            CreatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Projet créé : {Name}", name);
        return project;
    }

    public async Task<Project?> UpdateAsync(Guid id, string name, string description, DateTime? targetDate)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project == null) return null;

        project.Name = name;
        project.Description = description;
        project.TargetDate = targetDate.HasValue
    ? DateTime.SpecifyKind(targetDate.Value, DateTimeKind.Utc)
    : null;

        await _db.SaveChangesAsync();
        return project;
    }
}