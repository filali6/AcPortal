using Backend.Data;
using Backend.Modules.Events.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Events.Services;

public class EventsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<EventsService> _logger;

    public EventsService(AppDbContext db, ILogger<EventsService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // GET tous les événements
    public async Task<List<AcpEvent>> GetAllAsync()
    {
        return await _db.AcpEvents
            .OrderByDescending(e => e.ReceivedAt)
            .ToListAsync();
    }

    // GET un événement par ID
    public async Task<AcpEvent?> GetByIdAsync(Guid id)
    {
        return await _db.AcpEvents
            .FirstOrDefaultAsync(e => e.Id == id);
    }
}
