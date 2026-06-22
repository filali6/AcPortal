using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Events.Models;
using Backend.Modules.Projects.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend.Modules.Events.Services;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Backend.Modules.Tasks.Models;
using Backend.Modules.Git.Services;

namespace Backend.Modules.Projects.Controllers;

[ApiController]
[Route("api/streams")]
public class StreamController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EventPublisher _eventPublisher;
    private readonly GitService _gitService;

    public StreamController(AppDbContext db,EventPublisher eventPublisher,GitService gitService)
    {
        _db = db;
        _eventPublisher=eventPublisher;
        _gitService=gitService;
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
        // Ajouter Business Team
        foreach (var consultantId in dto.BusinessTeamConsultants)
        {
            _db.StreamMembers.Add(new StreamMember
            {
                StreamId = stream.Id,
                ConsultantId = consultantId,
                TeamType = TeamType.Business
            });
        }

        // Ajouter Technical Team
        foreach (var consultantId in dto.TechnicalTeamConsultants)
        {
            _db.StreamMembers.Add(new StreamMember
            {
                StreamId = stream.Id,
                ConsultantId = consultantId,
                TeamType = TeamType.Technical
            });
        }

        await _db.SaveChangesAsync();
        await _gitService.InitStreamRepoAsync(stream.Id, dto.ProjectId);
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
            .Include(s => s.Members)
                .ThenInclude(m => m.Consultant)
            .Include(s => s.BusinessTeamLead)
            .Include(s => s.TechnicalTeamLead)
            .Where(s => s.ProjectId == projectId)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.ProjectId,
                s.MessagingChannelId,
                s.MessagingChannelUrl,
                businessTeamLead = s.BusinessTeamLead == null ? null : new
                {
                    s.BusinessTeamLead.Id,
                    s.BusinessTeamLead.FullName
                },
                technicalTeamLead = s.TechnicalTeamLead == null ? null : new
                {
                    s.TechnicalTeamLead.Id,
                    s.TechnicalTeamLead.FullName
                },
                members = s.Members.Select(m => new
                {
                    m.Id,
                    m.ConsultantId,
                    fullName = m.Consultant.FullName,
                    email = m.Consultant.Email,
                    teamType = m.TeamType.ToString()
                })
            })
            .ToListAsync();
        return Ok(streams);
    }

 
    [HttpPost("{streamId}/members")]
    [Authorize(Roles = "ProjectManager")]
    public async Task<IActionResult> AddMember(
        Guid streamId,
        [FromBody] AddMemberDto dto)
    {
        var member = new StreamMember
        {
            StreamId = streamId,
            ConsultantId = dto.ConsultantId,
            TeamType=dto.TeamType
        };
        _db.StreamMembers.Add(member);
        await _db.SaveChangesAsync();
        return Ok(member);
    }
   
    [HttpGet("my")]
    [Authorize]
     
    public async Task<IActionResult> GetMyStreams()
    {
        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        if (user == null) return NotFound();

        var streams = await _db.Streams
            .Include(s => s.Members).ThenInclude(m => m.Consultant)
            .Include(s => s.BusinessTeamLead)
            .Include(s => s.TechnicalTeamLead)
            .Where(s => s.BusinessTeamLeadId == user.Id || s.TechnicalTeamLeadId == user.Id ||
        s.Members.Any(m => m.ConsultantId == user.Id))
            .Distinct()
            .ToListAsync();

        var streamIds = streams.Select(s => s.Id).ToList();
        var projectIds = streams.Select(s => s.ProjectId).Distinct().ToList();

        
        var stepIds = await _db.ProjectSteps
            .Where(s => s.StreamId != null && streamIds.Contains(s.StreamId.Value))
            .Select(s => s.Id)
            .ToListAsync();

       
        var tasks = await _db.AcpTasks
            .Where(t =>
                (t.StreamId != null && streamIds.Contains(t.StreamId.Value)) ||
                (t.StepId != null && stepIds.Contains(t.StepId.Value))
            )
            .ToListAsync();

        var assignedKeycloakIds = tasks.Where(t => t.AssignedTo != null).Select(t => t.AssignedTo!).Distinct().ToList();
        var users = await _db.Users.Where(u => assignedKeycloakIds.Contains(u.KeycloakId)).ToListAsync();

        var steps = await _db.ProjectSteps
            .Where(s => s.StreamId != null && streamIds.Contains(s.StreamId.Value))
            .ToListAsync();

        var projects = await _db.Projects
            .Where(p => projectIds.Contains(p.Id))
            .ToListAsync();

        var totalTasks = tasks.Count;
        var doneTasks = tasks.Count(t => t.Status == AcpTaskStatus.Done);

        return Ok(streams.Select(s => new
        {
            s.Id,
            s.Name,
            s.ProjectId,
            
            projectName = projects.FirstOrDefault(p => p.Id == s.ProjectId)?.Name ?? "—",
            s.MessagingChannelId,
            s.MessagingChannelUrl,
            businessTeamLead = s.BusinessTeamLead == null ? null : new
            {
                s.BusinessTeamLead.Id,
                s.BusinessTeamLead.FullName
            },
            technicalTeamLead = s.TechnicalTeamLead == null ? null : new
            {
                s.TechnicalTeamLead.Id,
                s.TechnicalTeamLead.FullName
            },
            members = s.Members.Select(m => new
            {
                m.Id,
                m.ConsultantId,
                fullName = m.Consultant.FullName,
                email = m.Consultant.Email,
                teamType = m.TeamType.ToString()
            }),
            steps = steps.Where(st => st.StreamId == s.Id).Select(st => new
            {
                st.Id,
                st.StepName,
                st.ToolName,
                st.Order,
                task = tasks.FirstOrDefault(t => t.StepId == st.Id) == null ? null : new
                {
                    tasks.FirstOrDefault(t => t.StepId == st.Id)!.Id,
                    tasks.FirstOrDefault(t => t.StepId == st.Id)!.Status,
                    assignedTo = tasks.FirstOrDefault(t => t.StepId == st.Id)!.AssignedTo,
                    assignedName = users
    .FirstOrDefault(u => u.KeycloakId == tasks.FirstOrDefault(t => t.StepId == st.Id)!.AssignedTo)
    ?.FullName ?? "—"
                }
            }).OrderBy(st => st.Order),
            streamProgress = tasks.Where(t => t.StreamId == s.Id).Count() > 0
                ? (int)Math.Round((double)tasks.Count(t => t.StreamId == s.Id && t.Status == AcpTaskStatus.Done)
                    / tasks.Count(t => t.StreamId == s.Id) * 100)
                : 0
        }));
    }
    [HttpDelete("{streamId}/members/{consultantId}")]
    [Authorize(Roles = "ProjectManager")]
    public async Task<IActionResult> RemoveMember(Guid streamId, Guid consultantId)
    {
        var member = await _db.StreamMembers
            .FirstOrDefaultAsync(m => m.StreamId == streamId && m.ConsultantId == consultantId);
        if (member == null) return NotFound();
        _db.StreamMembers.Remove(member);
        await _db.SaveChangesAsync();
        return Ok();
    }
    [HttpPatch("{streamId}/leads")]
    [Authorize(Roles = "ProjectManager")]
    public async Task<IActionResult> UpdateLeads(Guid streamId, [FromBody] UpdateLeadsDto dto)
    {
        var stream = await _db.Streams.FindAsync(streamId);
        if (stream == null) return NotFound();

        if (dto.BusinessTeamLeadId.HasValue)
            stream.BusinessTeamLeadId = dto.BusinessTeamLeadId;
        if (dto.TechnicalTeamLeadId.HasValue)
            stream.TechnicalTeamLeadId = dto.TechnicalTeamLeadId;

        await _db.SaveChangesAsync();
        return Ok(stream);
    }
    [HttpGet("{streamId}/members")]
    [Authorize]
    public async Task<IActionResult> GetMembers(Guid streamId)
    {
        var stream = await _db.Streams
            .Include(s => s.Members).ThenInclude(m => m.Consultant)
            .Include(s => s.BusinessTeamLead)
            .Include(s => s.TechnicalTeamLead)
            .FirstOrDefaultAsync(s => s.Id == streamId);

        if (stream == null) return NotFound();

        var members = new List<object>();

        if (stream.BusinessTeamLead != null)
            members.Add(new
            {
                keycloakId = stream.BusinessTeamLead.KeycloakId,
                fullName = stream.BusinessTeamLead.FullName,
                role = "Business Team Lead"
            });

        if (stream.TechnicalTeamLead != null)
            members.Add(new
            {
                keycloakId = stream.TechnicalTeamLead.KeycloakId,
                fullName = stream.TechnicalTeamLead.FullName,
                role = "Technical Team Lead"
            });

        foreach (var m in stream.Members)
            members.Add(new
            {
                keycloakId = m.Consultant.KeycloakId,
                fullName = m.Consultant.FullName,
                role = m.TeamType.ToString()
            });

        return Ok(members);
    }

    public class UpdateLeadsDto
    {
        public Guid? BusinessTeamLeadId { get; set; }
        public Guid? TechnicalTeamLeadId { get; set; }
    }

}

public class CreateStreamDto
{
    public string Name { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Guid? BusinessTeamLeadId { get; set; }
    public Guid? TechnicalTeamLeadId { get; set; }
    public List<Guid> BusinessTeamConsultants { get; set; } = new();
    public List<Guid> TechnicalTeamConsultants { get; set; } = new();
}

public class AddMemberDto
{
    public Guid ConsultantId { get; set; }
    public TeamType TeamType { get; set; }
}