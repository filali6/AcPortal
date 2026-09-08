using Backend.Data;
using Backend.Modules.Events.Models;
using Backend.Modules.Events.Handlers;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Teams.Services;

public class CreateTeamsChannelHandler : IActionHandler
{
    public string ActionType => "CREATE_TEAMS_CHANNEL";

    private readonly AppDbContext _db;
    private readonly GraphService _graphService;
    private readonly ILogger<CreateTeamsChannelHandler> _logger;

    public CreateTeamsChannelHandler(
        AppDbContext db,
        GraphService graphService,
        ILogger<CreateTeamsChannelHandler> logger)
    {
        _db = db;
        _graphService = graphService;
        _logger = logger;
    }

    public async Task HandleAsync(WorkflowRule rule, AcpEventDto eventDto, Guid? projectId)
    {
        if (!eventDto.StreamId.HasValue) return;

        var stream = await _db.Streams
            .FirstOrDefaultAsync(s => s.Id == eventDto.StreamId.Value);

        if (stream == null) return;

        // Get the project's Teams team ID
        var project = await _db.Projects
            .FirstOrDefaultAsync(p => p.Id == stream.ProjectId);

        if (project?.TeamsTeamId == null)
        {
            _logger.LogWarning("Project {ProjectId} has no Teams team — cannot create channel", stream.ProjectId);
            return;
        }

        // Create the channel
        var channelId = await _graphService.CreateChannelAsync(project.TeamsTeamId, stream.Name);

        if (channelId != null)
        {
            stream.TeamsChannelId = channelId;
            await _db.SaveChangesAsync();
            _logger.LogInformation("Teams channel created for stream {StreamId}", stream.Id);
        }
        else
        {
            _logger.LogWarning("Failed to create Teams channel for stream {StreamId}", stream.Id);
        }
    }
}