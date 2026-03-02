using Backend.Data;
using Backend.Modules.Tasks.Models;
using Microsoft.EntityFrameworkCore;
using Backend.Modules.Tasks.Models;
namespace Backend.Modules.Tasks.Services;

public class TasksService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TasksService> _logger;

    public TasksService(AppDbContext db, ILogger<TasksService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // GET toutes les tâches
    public async Task<List<AcpTask>> GetAllAsync()
    {
        return await _db.AcpTasks
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    // GET une tâche par ID
    public async Task<AcpTask?> GetByIdAsync(Guid id)
    {
        return await _db.AcpTasks
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    // PATCH changer le statut
    public async Task<AcpTask?> UpdateStatusAsync(Guid id, AcpTaskStatus newStatus)
    {
        var task = await _db.AcpTasks.FindAsync(id);
        if (task == null) return null;

        task.Status = newStatus;
        task.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Statut de la tâche {Id} changé vers {Status}", id, newStatus);
        return task;
    }

    // PATCH assigner à un utilisateur
    public async Task<AcpTask?> AssignAsync(Guid id, string assignedTo)
    {
        var task = await _db.AcpTasks.FindAsync(id);
        if (task == null) return null;

        task.AssignedTo = assignedTo;
        task.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Tâche {Id} assignée à {User}", id, assignedTo);
        return task;
    }
}