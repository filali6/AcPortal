using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Events.Models;
using Backend.Modules.Projects.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Backend.Modules.Projects.Controllers;

[ApiController]
[Route("api/streams")]
public class StreamController : ControllerBase
{
    private readonly AppDbContext _db;

    public StreamController(AppDbContext db)
    {
        _db = db;
    }

    // PROJECT MANAGER — créer un stream et assigner les leads
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

        // publier event StreamCréé → déclenche tâches pour les leads
        var eventPayload = JsonSerializer.Serialize(new AcpEventDto
        {
            EventType = "StreamCréé",
            ProjectId = dto.ProjectId,
            StreamId = stream.Id,
            BusinessTeamLeadId = dto.BusinessTeamLeadId,
            TechnicalTeamLeadId = dto.TechnicalTeamLeadId
        });

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Topic = $"project.{dto.ProjectId}",
            Payload = eventPayload
        });

        await _db.SaveChangesAsync();

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
        var userIdClaim = User.FindFirst("id")?.Value;
        if (userIdClaim == null) return Unauthorized();
        var userId = Guid.Parse(userIdClaim);

        var streams = await _db.Streams
            .Where(s =>
                s.BusinessTeamLeadId == userId ||
                s.TechnicalTeamLeadId == userId)
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