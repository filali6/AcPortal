using Backend.Data;
using Backend.Modules.Events.Models;
using Backend.Modules.Events.Handlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Models.ExternalConnectors;

namespace Backend.Modules.Teams.Services;

public class CreateTeamsTeamHandler : IActionHandler
{
    public string ActionType => "CREATE_TEAMS_TEAM";

    private readonly AppDbContext _db;
    private readonly GraphService _graphService;
    private readonly ILogger<CreateTeamsTeamHandler> _logger;
    private readonly IConfiguration _configuration;

    public CreateTeamsTeamHandler(
        AppDbContext db,
        GraphService graphService,
        ILogger<CreateTeamsTeamHandler> logger, IConfiguration configuration)
    {
        _db = db;
        _graphService = graphService;
        _logger = logger;
        _configuration=configuration;
    }

    public async Task HandleAsync(WorkflowRule rule, AcpEventDto eventDto, Guid? projectId)
    {
        if (projectId == null) return;

        var project = await _db.Projects
            .Include(p => p.ProjectManager)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null) return;

        // Get owner email — use HeadOfCDS or ProjectManager
        var ownerEmail = _configuration["MicrosoftGraph:OwnerEmail"];
        if (ownerEmail == null)
        {
            // fallback — get from event
            var headOfCds = await _db.Users
                .FirstOrDefaultAsync(u => u.Role == Backend.Modules.Auth.Models.GlobalRole.HeadOfCDS);
            ownerEmail = headOfCds?.Email;
        }

        if (ownerEmail == null)
        {
            _logger.LogWarning("No owner email found for project {ProjectId}", projectId);
            project.TeamsSetupFailed = true;
            await _db.SaveChangesAsync();
            return;
        }

        // Create the Teams team
        var teamId = await _graphService.CreateTeamAsync(project.Name, ownerEmail);

        if (teamId != null)
        {
            project.TeamsTeamId = teamId;
            project.TeamsSetupFailed = false;
            _logger.LogInformation("Teams team created for project {ProjectId}", projectId);
        }
        else
        {
            project.TeamsSetupFailed = true;
            _logger.LogWarning("Failed to create Teams team for project {ProjectId}", projectId);
        }

        await _db.SaveChangesAsync();
    }
}