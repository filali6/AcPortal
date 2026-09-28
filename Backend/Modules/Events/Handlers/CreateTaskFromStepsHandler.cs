using Backend.Data;
using Backend.Modules.Events.Models;
using Backend.Modules.Projects.Models;
using Microsoft.EntityFrameworkCore;
using Backend.Modules.Tasks.Models;
using Backend.Modules.Sla.Services;

namespace Backend.Modules.Events.Handlers;

public class CreateTasksFromStepsHandler : IActionHandler
{
    public string ActionType => "CREATE_TASKS_FROM_STEPS";
    private readonly AppDbContext _db;
    private readonly ILogger<CreateTasksFromStepsHandler> _logger;
    private readonly SlaCheckerService _slaChecker;

    public CreateTasksFromStepsHandler(
        AppDbContext db,
        ILogger<CreateTasksFromStepsHandler> logger,
        SlaCheckerService slaChecker)
    {
        _db = db;
        _logger = logger;
        _slaChecker = slaChecker;
    }

    public async Task HandleAsync(WorkflowRule rule, AcpEventDto eventDto, Guid? projectId)
    {
        var streamId = eventDto.StreamId;

        if (streamId == null || projectId == null)
        {
            _logger.LogWarning("CreateTasksFromStepsHandler: streamId or projectId is null");
            return;
        }

        var teamType = eventDto.LeadRole == "BusinessTeamLead"
            ? TeamType.Business
            : TeamType.Technical;

        var steps = await _db.ProjectSteps
            .Where(s => s.StreamId == streamId)
            .ToListAsync();

        if (!steps.Any())
        {
            _logger.LogWarning("No steps found for stream {StreamId}", streamId);
            return;
        }

        var stream = await _db.Streams.FindAsync(streamId);

        // Point de départ du calendrier : maintenant, ou la StartDate du projet si elle est dans le futur
        var now = DateTime.UtcNow;
        var projectStart = await _db.Projects
            .Where(p => p.Id == projectId.Value)
            .Select(p => p.StartDate)
            .FirstOrDefaultAsync();
        var scheduleOrigin = projectStart.HasValue && projectStart.Value > now
            ? projectStart.Value
            : now;

        foreach (var step in steps)
        {
            var stepTeamType = step.TeamType ?? teamType;

            var assignedKeycloakId = await FindBestConsultantKeycloakIdAsync(
                projectId.Value, streamId.Value, stepTeamType);

            if (assignedKeycloakId == null)
            {
                _logger.LogWarning("No consultant found for step {StepName} teamType {TeamType}",
                    step.StepName, stepTeamType);
                continue;
            }

            // Avec estimation → DueDate fixée ici, la règle SLA générique ne la touchera pas
            //   (ApplySlaRulesToTasksAsync ne traite que DueDate == null).
            // Sans estimation → DueDate reste null, comportement actuel inchangé.
            // Chaque tâche est indépendante : pas de cumul entre steps.
            DateTime? dueDate = step.EstimatedDays is int days && days > 0
                ? scheduleOrigin.AddDays(days)
                : null;

            _db.AcpTasks.Add(new AcpTask
            {
                Title = step.StepName,
                ToolName = step.ToolName,
                AssignedTo = assignedKeycloakId,
                StreamId = stream?.Id,
                ProjectId = projectId.Value,
                StepId = step.Id,
                Status = 0,
                CreatedAt = now,
                DueDate = dueDate
            });

            _logger.LogInformation(
                "Tâche créée : {StepName} → {Consultant} (TeamType: {TeamType}, DueDate: {DueDate})",
                step.StepName, assignedKeycloakId, stepTeamType,
                dueDate?.ToString("yyyy-MM-dd") ?? "SLA générique");
        }

        await _db.SaveChangesAsync();

        // Tâches sans estimation → règle SLA appliquée tout de suite (plus besoin de "Apply rules")
        await _slaChecker.ApplySlaRulesAsync();
    }

    private async Task<string?> FindBestConsultantKeycloakIdAsync(
        Guid projectId, Guid streamId, TeamType teamType)
    {
        var memberIds = await _db.StreamMembers
            .Where(m => m.StreamId == streamId && m.TeamType == teamType)
            .Select(m => m.ConsultantId)
            .ToListAsync();

        if (!memberIds.Any())
            return null;

        var bestConsultant = await _db.Users
            .Where(u => memberIds.Contains(u.Id))
            .Select(u => new
            {
                u.KeycloakId,
                ActiveTasks = _db.AcpTasks.Count(t =>
                    t.AssignedTo == u.KeycloakId && t.Status != AcpTaskStatus.Done)
            })
            .OrderBy(u => u.ActiveTasks)
            .FirstOrDefaultAsync();

        return bestConsultant?.KeycloakId;
    }
}