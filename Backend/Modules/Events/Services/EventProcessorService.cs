using Backend.Data;
using Backend.Modules.Events.Models;
using Backend.Modules.Tasks.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Backend.Hubs;
using System.Text.Json;

namespace Backend.Modules.Events.Services;

public class EventProcessorService
{
    private readonly AppDbContext _db;
    private readonly ILogger<EventProcessorService> _logger;

    private readonly IHubContext<NotificationHub> _hubContext;

    public EventProcessorService(AppDbContext db, ILogger<EventProcessorService> logger, IHubContext<NotificationHub> hubContext)
    {
        _db = db;
        _logger = logger;
        _hubContext=hubContext;
    }

    public async Task ProcessAsync(string messageValue, Guid? projectId = null)
    {
       
        var eventDto = JsonSerializer.Deserialize<AcpEventDto>(messageValue,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (eventDto == null)
        {
            _logger.LogWarning("Message invalide reçu — impossible de désérialiser.");
            return;
        }

         
        // if (string.IsNullOrWhiteSpace(eventDto.ToolName) ||
        //     string.IsNullOrWhiteSpace(eventDto.EventType))
        // {
        //     _logger.LogWarning("Message ignoré — ToolName ou EventType manquant.");
        //     return;
        // }

        switch (eventDto.EventType)
        {
            case "ContratSigné":
               
                var superAdmin = await _db.Users
                    .FirstOrDefaultAsync(u => u.Role == Backend.Modules.Auth.Models.GlobalRole.SuperAdmin);
                await CreateTaskForRoleAsync(
                    "Créer le projet",
                    "Un contrat a été signé — créer le projet correspondant",
                    "portal",
                    superAdmin?.Id,   
                    null,
                    projectId);
                break;

            case "ProjetCréé":
                await CreateTaskForUserAsync(
                    "Créer l'équipe du projet",
                    "Le projet a été créé — constituer l'équipe et désigner un Chef d'Équipe",
                    "portal", eventDto.DirectorId, projectId);
                break;

            case "EquipeCréée":
                await CreateTaskForUserAsync(
                    "Définir les steps du projet",
                    "L'équipe est constituée — définir les étapes du projet",
                    "portal", eventDto.ChefEquipeId, projectId);
                break;

            case "StepsDéfinis":
                await HandleStepsDéfinisAsync(projectId);
                break;

            case "TâcheTerminée":
                await HandleTâcheTerminéeAsync(eventDto.StepId, projectId);
                break;

            default:
                
                await HandlePluginEventAsync(eventDto, projectId);
                break;
        }}
    private async Task<string?> FindBestConsultantAsync(string toolName, Guid? projectId = null)
    {
        if (projectId == null) return null;

        // Récupérer les membres de l'équipe du projet (sans le chef)
        var team = await _db.Teams
            .FirstOrDefaultAsync(t => t.ProjectId == projectId);

        if (team == null) return null;

        var members = await _db.TeamMembers
            .Where(m => m.TeamId == team.Id && m.ConsultantId != team.ChefEquipeId)
            .Select(m => m.ConsultantId)
            .ToListAsync();

        if (!members.Any()) return null;

        // Celui qui a le moins de tâches actives dans ce projet
        string? bestName = null;
        int minTasks = int.MaxValue;

        foreach (var memberId in members)
        {
            var consultant = await _db.Users.FindAsync(memberId);
            if (consultant == null) continue;

            var count = await _db.AcpTasks
                .CountAsync(t => t.AssignedTo == consultant.FullName
                              && t.ProjectId == projectId
                              && t.Status != AcpTaskStatus.Done);

            if (count < minTasks)
            {
                minTasks = count;
                bestName = consultant.FullName;
            }
        }

        return bestName;
    }


    private async Task CreateTaskForUserAsync(
        string title, string description, string toolName,
        Guid? userId, Guid? projectId)
    {
        if (userId == null) return;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;

        var acpEvent = new AcpEvent { ToolName = toolName, EventType = title, ReceivedAt = DateTime.UtcNow };
        var acpTask = new AcpTask
        {
            Title = title,
            Description = description,
            ToolName = toolName,
            AssignedTo = user.FullName,
            Status = AcpTaskStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            SourceEventId = acpEvent.Id,
            ProjectId = projectId
        };
        acpEvent.GeneratedTaskId = acpTask.Id;
        _db.AcpEvents.Add(acpEvent);
        _db.AcpTasks.Add(acpTask);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Tâche créée pour {User} : {Title}", user.FullName, title);
        await _hubContext.Clients
            .Group(user.Id.ToString())
            .SendAsync("NewNotification", new
            {
                message = title,
                projectId = projectId
            });
    }

    private async Task CreateTaskForRoleAsync(
        string title, string description, string toolName,
        Guid? userId, Guid? stepId, Guid? projectId)
    {
        // var superAdmin = await _db.Users
        //     .FirstOrDefaultAsync(u => u.Role == Backend.Modules.Auth.Models.GlobalRole.SuperAdmin);
        // if (superAdmin == null) return;
        if (userId == null) return;
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return;

        var acpEvent = new AcpEvent { ToolName = toolName, EventType = title, ReceivedAt = DateTime.UtcNow };
        var acpTask = new AcpTask
        {
            Title = title,
            Description = description,
            ToolName = toolName,
            AssignedTo = user.FullName,
            Status = AcpTaskStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            SourceEventId = acpEvent.Id,
            ProjectId = projectId,
            StepId = stepId
        };
        acpEvent.GeneratedTaskId = acpTask.Id;
        _db.AcpEvents.Add(acpEvent);
        _db.AcpTasks.Add(acpTask);
        await _db.SaveChangesAsync();
        await _hubContext.Clients
            .Group(user.Id.ToString())
            .SendAsync("NewNotification", new
            {
                message = title,
                projectId = projectId
            });
        _logger.LogInformation("Tâche créée pour SuperAdmin : {Title}", title);
    }

    private async Task HandleStepsDéfinisAsync(Guid? projectId)
    {
        if (projectId == null) return;
        var steps = await _db.ProjectSteps
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.Order)
            .ToListAsync();

        foreach (var step in steps)
        {
            var taskExists = await _db.AcpTasks.AnyAsync(t => t.StepId == step.Id);
            if (taskExists) continue;
            _logger.LogInformation("Step: {Name} — DependsOnStepId: {Dep}", step.StepName, step.DependsOnStepId);

            var status = step.DependsOnStepId.HasValue ? AcpTaskStatus.Blocked : AcpTaskStatus.Pending;
            var assignedTo = await FindBestConsultantAsync(step.ToolName, projectId);

            var acpEvent = new AcpEvent { ToolName = step.ToolName, EventType = "StepTask", ReceivedAt = DateTime.UtcNow };
            var acpTask = new AcpTask
            {
                Title = step.StepName,
                Description = $"Tâche depuis step : {step.StepName}",
                ToolName = step.ToolName,
                AssignedTo = assignedTo ?? "Non assigné",
                Status = status,
                CreatedAt = DateTime.UtcNow,
                SourceEventId = acpEvent.Id,
                ProjectId = projectId,
                StepId = step.Id
            };
            acpEvent.GeneratedTaskId = acpTask.Id;
            _db.AcpEvents.Add(acpEvent);
            _db.AcpTasks.Add(acpTask);
            var consultant = await _db.Users
            .FirstOrDefaultAsync(u => u.FullName == assignedTo);
            if (consultant != null)
            {
                await _hubContext.Clients
                    .Group(consultant.Id.ToString())
                    .SendAsync("NewNotification", new
                    {
                        message = $"Nouvelle tâche assignée : {step.StepName}",
                        projectId = projectId
                    });
            }
        }
        
    
    await _db.SaveChangesAsync();
        _logger.LogInformation("Tâches créées depuis les steps du projet {ProjectId}", projectId);
    }

    private async Task HandleTâcheTerminéeAsync(Guid? stepId, Guid? projectId)
    {
        _logger.LogInformation("HandleTâcheTerminée appelé — stepId: {StepId}", stepId);
        if (stepId == null) return;
        var dependentSteps = await _db.ProjectSteps
            .Where(s => s.DependsOnStepId == stepId)
            .ToListAsync();

        _logger.LogInformation("Steps dépendants trouvés: {Count}", dependentSteps.Count);

        foreach (var depStep in dependentSteps)
        {
            var task = await _db.AcpTasks.FirstOrDefaultAsync(t => t.StepId == depStep.Id);
            _logger.LogInformation("Tâche trouvée: {Title} — Status: {Status}", task?.Title, task?.Status);
            if (task == null || task.Status != AcpTaskStatus.Blocked) continue;
            task.Status = AcpTaskStatus.Pending;
            task.UpdatedAt = DateTime.UtcNow;

            var consultant = await _db.Users
        .FirstOrDefaultAsync(u => u.FullName == task.AssignedTo);
            if (consultant != null)
            {
                await _hubContext.Clients
                    .Group(consultant.Id.ToString())
                    .SendAsync("NewNotification", new
                    {
                        message = $"Tâche débloquée : {task.Title}",
                        projectId = projectId
                    });
            }
            _logger.LogInformation("Tâche débloquée : {Title}", task.Title);
        }
        await _db.SaveChangesAsync();
    }

    private async Task HandlePluginEventAsync(AcpEventDto eventDto, Guid? projectId)
    {
        var exists = await _db.AcpEvents.AnyAsync(e =>
            e.ToolName == eventDto.ToolName &&
            e.EventType == eventDto.EventType &&
            e.ReceivedAt >= DateTime.UtcNow.AddSeconds(-5));
        if (exists) { _logger.LogWarning("Doublon détecté."); return; }

        var assignedTo = await FindBestConsultantAsync(eventDto.ToolName);
        var acpEvent = new AcpEvent { ToolName = eventDto.ToolName, EventType = eventDto.EventType, ReceivedAt = DateTime.UtcNow };
        var acpTask = new AcpTask
        {
            Title = $"{eventDto.EventType} — {eventDto.ToolName}",
            Description = $"Tâche générée depuis {eventDto.ToolName}",
            ToolName = eventDto.ToolName,
            AssignedTo = assignedTo ?? "Non assigné",
            Status = AcpTaskStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            SourceEventId = acpEvent.Id,
            ProjectId = projectId
        };
        acpEvent.GeneratedTaskId = acpTask.Id;
        _db.AcpEvents.Add(acpEvent);
        _db.AcpTasks.Add(acpTask);
        await _db.SaveChangesAsync();
        _logger.LogInformation("Tâche créée : {Title}", acpTask.Title);
    }

 
    private class ConsultantMetric
{
    public Guid ConsultantId { get; set; }
    public int  TaskCount    { get; set; }
    public int  RolesInTool  { get; set; }
}

}