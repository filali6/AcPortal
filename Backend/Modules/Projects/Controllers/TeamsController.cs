using Backend.Modules.Projects.Services;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Backend.Modules.Projects.Controllers;

[ApiController]
[Route("api/teams")]
public class TeamsController : ControllerBase
{
    private readonly TeamsService _teamsService;

    public TeamsController(TeamsService teamsService)
    {
        _teamsService = teamsService;
    }

    // POST api/teams
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTeamRequest request)
    {
        var team = await _teamsService.CreateAsync(
            request.Name,
            request.ProjectId,
            request.ChefEquipeId
        );

        if (team == null)
            return BadRequest(new { message = "Projet introuvable ou équipe déjà existante" });

        return Ok(new
        {
            message = "Equipe créée avec succès",
            data = team
        });
    }

    // GET api/teams/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var team = await _teamsService.GetByIdAsync(id);
        if (team == null)
            return NotFound(new { message = "Equipe introuvable" });

        return Ok(team);
    }

    // GET api/teams/{id}/members
    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        var members = await _teamsService.GetMembersAsync(id);
        return Ok(members);
    }

    // POST api/teams/{id}/members
    [HttpPost("{id:guid}/members")]
     
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