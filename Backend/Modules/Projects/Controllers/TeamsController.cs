using Backend.Modules.Projects.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Backend.Data;
using Backend.Modules.Events.Models;
using Microsoft.EntityFrameworkCore;
using Backend.Modules.Events.Services;


namespace Backend.Modules.Projects.Controllers;

[ApiController]
[Route("api/teams")]
public class TeamsController : ControllerBase
{
    private readonly TeamsService _teamsService;
    private readonly AppDbContext _db;
    private readonly EventPublisher _eventPublisher;

    public TeamsController(TeamsService teamsService, AppDbContext db,EventPublisher eventPublisher)
    {
        _teamsService = teamsService;
        _db=db;
        _eventPublisher=eventPublisher;
    }

    
    [HttpPost]
    [Authorize(Roles = "HeadOfCDS,PortfolioDirector")]
    public async Task<IActionResult> Create([FromBody] CreateTeamRequest request)
    {
        var team = await _teamsService.CreateAsync(
            request.Name,
            request.ProjectId,
            request.ChefEquipeId
        );

        if (team == null)
            return BadRequest(new { message = "Projet introuvable ou équipe déjà existante" });

        await _eventPublisher.PublishAsync(new
        {
            eventType = "EquipeCréée",
            chefEquipeId = request.ChefEquipeId,
            projectId = request.ProjectId
        }, request.ProjectId);

        return Ok(new
        {
            message = "Equipe créée avec succès",
            data = team
        });
    }

     
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "HeadOfCDS,PortfolioDirector")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var team = await _teamsService.GetByIdAsync(id);
        if (team == null)
            return NotFound(new { message = "Equipe introuvable" });

        return Ok(team);
    }

    
    [HttpGet("{id:guid}/members")]
    [Authorize(Roles = "HeadOfCDS,PortfolioDirector")]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        var members = await _teamsService.GetMembersAsync(id);
        return Ok(members);
    }

    
    [HttpPost("{id:guid}/members")]
    [Authorize(Roles = "HeadOfCDS,PortfolioDirector")]

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
    [Authorize(Roles = "HeadOfCDS,PortfolioDirector")]
    public async Task<IActionResult> GetByProject(Guid projectId)
    {
        var team = await _teamsService.GetByProjectAsync(projectId);

        if (team == null)
            return NotFound(new { message = "Aucune équipe pour ce projet" });

        return Ok(team);
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