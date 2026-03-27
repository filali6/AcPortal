using Backend.Modules.Projects.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Backend.Data;
using Backend.Modules.Events.Models;
using Microsoft.EntityFrameworkCore;


namespace Backend.Modules.Projects.Controllers;

[ApiController]
[Route("api/teams")]
public class TeamsController : ControllerBase
{
    private readonly TeamsService _teamsService;
    private readonly AppDbContext _db;

    public TeamsController(TeamsService teamsService, AppDbContext db)
    {
        _teamsService = teamsService;
        _db=db;
    }

    
    [HttpPost]
    [Authorize(Roles = "SuperAdmin,PortfolioDirector")]
    public async Task<IActionResult> Create([FromBody] CreateTeamRequest request)
    {
        var team = await _teamsService.CreateAsync(
            request.Name,
            request.ProjectId,
            request.ChefEquipeId
        );

        if (team == null)
            return BadRequest(new { message = "Projet introuvable ou équipe déjà existante" });

        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            eventType = "EquipeCréée",
            chefEquipeId = request.ChefEquipeId,
            projectId = request.ProjectId
        });

        _db.OutboxMessages.Add(new Backend.Modules.Events.Models.OutboxMessage
        {
            Topic = $"project.{request.ProjectId}",
            Payload = payload,
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            Retries = 0
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Equipe créée avec succès",
            data = team
        });
    }

     
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,PortfolioDirector")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var team = await _teamsService.GetByIdAsync(id);
        if (team == null)
            return NotFound(new { message = "Equipe introuvable" });

        return Ok(team);
    }

    
    [HttpGet("{id:guid}/members")]
    [Authorize(Roles = "SuperAdmin,PortfolioDirector")]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        var members = await _teamsService.GetMembersAsync(id);
        return Ok(members);
    }

    
    [HttpPost("{id:guid}/members")]
    [Authorize(Roles = "SuperAdmin,PortfolioDirector")]

    public async Task<IActionResult> AddMembers(Guid id, [FromBody] AddMemberRequest request)
    {
        var members = await _teamsService.AddMembersAsync(id, request.ConsultantIds);

        if (!members.Any())
            return BadRequest(new { message = "Aucun consultant ajouté" });

        return Ok(new
        {
            message = $"{members.Count} consultant(s) ajouté(s) avec succès",
            data = members
        });
    }
    [HttpGet("project/{projectId:guid}")]
    [Authorize(Roles = "SuperAdmin,PortfolioDirector")]
    public async Task<IActionResult> GetByProject(Guid projectId)
    {
        var team = await _teamsService.GetByProjectAsync(projectId);

        if (team == null)
            return NotFound(new { message = "Aucune équipe pour ce projet" });

        return Ok(team);
    }
    [HttpGet("is-chef-equipe")]
    [Authorize]
    public async Task<IActionResult> IsChefEquipe()
    {
        var userId = Guid.Parse(User.FindFirst("id")!.Value);
        var isChef = await _db.Teams.AnyAsync(t => t.ChefEquipeId == userId);
        return Ok(new { isChefEquipe = isChef });
    }
    [HttpGet("my-chef-equipe-projects")]
    [Authorize]
    public async Task<IActionResult> GetMyChefEquipeProjects()
    {
        var userId = Guid.Parse(User.FindFirst("id")!.Value);

        var projects = await _db.Teams
            .Where(t => t.ChefEquipeId == userId)
            .Join(_db.Projects,
                t => t.ProjectId,
                p => p.Id,
                (t, p) => new { id = p.Id, name = p.Name })
            .ToListAsync();

        return Ok(projects);
    }




    public class CreateTeamRequest
{
    [Required(ErrorMessage = "Name est obligatoire")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "ProjectId est obligatoire")]
    public Guid ProjectId { get; set; }

    [Required(ErrorMessage = "ChefEquipeId est obligatoire")]
    public Guid ChefEquipeId { get; set; }
}


public class AddMemberRequest
{
    [Required(ErrorMessage = "ConsultantId est obligatoire")]
        public List<Guid> ConsultantIds { get; set; } = new();}
}