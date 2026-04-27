using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Events.Models;
using Backend.Modules.Projects.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend.Modules.Events.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Backend.Modules.Projects.Controllers;

[ApiController]
[Route("api/streams")]
public class StreamController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EventPublisher _eventPublisher;

    public StreamController(AppDbContext db,EventPublisher eventPublisher)
    {
        _db = db;
        _eventPublisher=eventPublisher;
    }

     
    [HttpPost]
    [Authorize(Roles = "ProjectManager")]
    public async Task<IActionResult> Create(
        [FromBody] CreateStreamDto dto)
    {
        var stream = new Backend.Modules.Projects.Models.Stream
        {
            Name = dto.Name,
            ProjectId = dto.ProjectId,
            BusinessTeamLeadId = dto.BusinessTeamLeadId,
            TechnicalTeamLeadId = dto.TechnicalTeamLeadId
        };

        _db.Streams.Add(stream);
        await _db.SaveChangesAsync();
        var project = await _db.Projects.FindAsync(dto.ProjectId);

        await _eventPublisher.PublishAsync(new
        {
            eventType = "StreamCréé",
            projectId = dto.ProjectId,
            projectName = project!.Name,
            streamId = stream.Id,
            businessTeamLeadId = dto.BusinessTeamLeadId,
            technicalTeamLeadId = dto.TechnicalTeamLeadId
        }, dto.ProjectId, project.Name);



        return Ok(stream);
    }

    // GET streams d'un projet
    [HttpGet("project/{projectId}")]
    [Authorize]
    public async Task<IActionResult> GetByProject(Guid projectId)
    {
        var streams = await _db.Streams
            .Where(s => s.ProjectId == projectId)
            .ToListAsync();
        return Ok(streams);
    }

    // AJOUTER un consultant à un stream
    [HttpPost("{streamId}/members")]
    [Authorize(Roles = "ProjectManager")]
    public async Task<IActionResult> AddMember(
        Guid streamId,
        [FromBody] AddMemberDto dto)
    {
        var member = new StreamMember
        {
            StreamId = streamId,
            ConsultantId = dto.ConsultantId
        };
        _db.StreamMembers.Add(member);
        await _db.SaveChangesAsync();
        return Ok(member);
    }
    // retourne les streams où le user connecté est lead
    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMyStreams()
    {
        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        if (user == null) return NotFound();

        var streams = await _db.Streams
            .Where(s =>
                s.BusinessTeamLeadId == user.Id ||
                s.TechnicalTeamLeadId == user.Id)
            .Distinct()
            .ToListAsync();

        return Ok(streams);
    }

}

public class CreateStreamDto
{
    public string Name { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Guid? BusinessTeamLeadId { get; set; }
    public Guid? TechnicalTeamLeadId { get; set; }
}

public class AddMemberDto
{
    public Guid ConsultantId { get; set; }
}