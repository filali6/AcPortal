using Backend.Data;
using Backend.Modules.Tasks.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Projects.Services;


public class ProjectStatusService
{
    private readonly AppDbContext _db;

    public ProjectStatusService(AppDbContext db)
    {
        _db = db;
    }

    // Un stream est actif s'il a au moins une tâche Pending ou Blocked.
    // Un stream SANS AUCUNE tâche est considéré actif par défaut : le travail n'a
    // simplement pas encore démarré, il ne faut surtout pas le compter comme "terminé"
    // (ça libérerait à tort la charge de son lead/ses consultants).
    public async Task<bool> IsStreamActiveAsync(Guid streamId)
    {
        var statuses = await _db.AcpTasks
            .Where(t => t.StreamId == streamId)
            .Select(t => t.Status)
            .ToListAsync();

        if (!statuses.Any()) return true;
        return statuses.Any(s => s != AcpTaskStatus.Done);
    }

    public async Task<bool> IsProjectActiveAsync(Guid projectId)
    {
        var statuses = await _db.AcpTasks
            .Where(t => t.ProjectId == projectId)
            .Select(t => t.Status)
            .ToListAsync();

        if (!statuses.Any()) return true;
        return statuses.Any(s => s != AcpTaskStatus.Done);
    }

    // Version "bulk" — évite le problème N+1 quand on doit calculer l'activité de
    // plusieurs streams d'un coup (typiquement : la charge de travail de chaque
    // lead/consultant dans get_planning_context()).
    public async Task<HashSet<Guid>> GetActiveStreamIdsAsync(IEnumerable<Guid> streamIds)
    {
        var ids = streamIds.Distinct().ToList();
        if (!ids.Any()) return new HashSet<Guid>();

        var grouped = await _db.AcpTasks
            .Where(t => t.StreamId != null && ids.Contains(t.StreamId.Value))
            .GroupBy(t => t.StreamId!.Value)
            .Select(g => new
            {
                StreamId = g.Key,
                AllDone = g.All(t => t.Status == AcpTaskStatus.Done)
            })
            .ToListAsync();

        var streamsWithTasks = grouped.Select(g => g.StreamId).ToHashSet();
        var activeWithTasks = grouped.Where(g => !g.AllDone).Select(g => g.StreamId);
        var streamsWithoutTasks = ids.Where(id => !streamsWithTasks.Contains(id));

        return activeWithTasks.Concat(streamsWithoutTasks).ToHashSet();
    }

    // Charge de travail : userId → nombre de streams ACTIFS où la personne est
    // lead (Business ou Technical) ou consultant membre.
    // Une personne absente du dictionnaire = 0 stream actif.
    public async Task<Dictionary<Guid, int>> GetActiveStreamCountByUserAsync()
    {
        var streams = await _db.Streams
            .AsNoTracking()
            .Select(s => new { s.Id, s.BusinessTeamLeadId, s.TechnicalTeamLeadId })
            .ToListAsync();

        var members = await _db.StreamMembers
            .AsNoTracking()
            .Select(m => new { m.StreamId, m.ConsultantId })
            .ToListAsync();

        var activeIds = await GetActiveStreamIdsAsync(streams.Select(s => s.Id));

        // Une personne compte 1 par stream actif, même si elle y a plusieurs rôles
        var pairs = new HashSet<(Guid userId, Guid streamId)>();

        foreach (var s in streams.Where(s => activeIds.Contains(s.Id)))
        {
            if (s.BusinessTeamLeadId.HasValue) pairs.Add((s.BusinessTeamLeadId.Value, s.Id));
            if (s.TechnicalTeamLeadId.HasValue) pairs.Add((s.TechnicalTeamLeadId.Value, s.Id));
        }

        foreach (var m in members.Where(m => activeIds.Contains(m.StreamId)))
            pairs.Add((m.ConsultantId, m.StreamId));

        return pairs
            .GroupBy(p => p.userId)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}