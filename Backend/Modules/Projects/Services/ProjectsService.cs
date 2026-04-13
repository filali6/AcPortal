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
}