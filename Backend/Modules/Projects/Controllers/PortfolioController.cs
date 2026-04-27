using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Projects.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Backend.Modules.Projects.Controllers;

[ApiController]
[Route("api/portfolios")]
public class PortfoliosController : ControllerBase
{
    private readonly AppDbContext _db;

    public PortfoliosController(AppDbContext db)
    {
        _db = db;
    }

    // Créer un portfolio + assigner un PD
    [HttpPost]
    [Authorize(Roles = "HeadOfCDS")]
    public async Task<IActionResult> Create([FromBody] CreatePortfolioRequest request)
    {
        // Vérifier que le PD existe et a le bon rôle
        var director = await _db.Users.FirstOrDefaultAsync(u =>
            u.Id == request.PortfolioDirectorId &&
            u.Role == GlobalRole.PortfolioDirector);

        if (director == null)
            return BadRequest(new { message = "PortfolioDirector introuvable ou rôle incorrect" });

        var portfolio = new Portfolio
        {
            Name = request.Name,
            Description = request.Description,
            PortfolioDirectorId = request.PortfolioDirectorId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Portfolios.Add(portfolio);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Portfolio créé avec succès",
            data = new
            {
                portfolio.Id,
                portfolio.Name,
                portfolio.Description,
                portfolio.CreatedAt,
                director = new { director.Id, director.FullName, director.Email }
            }
        });
    }

    // Lister tous les portfolios
    [HttpGet]
    [Authorize(Roles = "HeadOfCDS,PortfolioDirector")]
    public async Task<IActionResult> GetAll()
    {
        var portfolios = await _db.Portfolios
            .Include(p => p.PortfolioDirector)
            .Include(p => p.Projects)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.CreatedAt,
                director = p.PortfolioDirector == null ? null : new
                {
                    p.PortfolioDirector.Id,
                    p.PortfolioDirector.FullName,
                    p.PortfolioDirector.Email
                },
                projectCount = p.Projects.Count
            })
            .ToListAsync();

        return Ok(portfolios);
    }

    // Détail d'un portfolio
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "HeadOfCDS,PortfolioDirector")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var portfolio = await _db.Portfolios
            .Include(p => p.PortfolioDirector)
            .Include(p => p.Projects)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (portfolio == null)
            return NotFound(new { message = "Portfolio introuvable" });

        return Ok(new
        {
            portfolio.Id,
            portfolio.Name,
            portfolio.Description,
            portfolio.CreatedAt,
            director = portfolio.PortfolioDirector == null ? null : new
            {
                portfolio.PortfolioDirector.Id,
                portfolio.PortfolioDirector.FullName,
                portfolio.PortfolioDirector.Email
            },
            projects = portfolio.Projects.Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.CreatedAt
            })
        });
    }

    // Changer le PD d'un portfolio
    [HttpPatch("{id:guid}/director")]
    [Authorize(Roles = "HeadOfCDS")]
    public async Task<IActionResult> AssignDirector(Guid id, [FromBody] AssignDirectorRequest request)
    {
        var portfolio = await _db.Portfolios.FindAsync(id);
        if (portfolio == null)
            return NotFound(new { message = "Portfolio introuvable" });

        var director = await _db.Users.FirstOrDefaultAsync(u =>
            u.Id == request.PortfolioDirectorId &&
            u.Role == GlobalRole.PortfolioDirector);

        if (director == null)
            return BadRequest(new { message = "PortfolioDirector introuvable ou rôle incorrect" });

        portfolio.PortfolioDirectorId = request.PortfolioDirectorId;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Portfolio Director mis à jour",
            data = new
            {
                portfolio.Id,
                portfolio.Name,
                director = new { director.Id, director.FullName, director.Email }
            }
        });
    }

    // Mes portfolios (pour le PortfolioDirector connecté)
    [HttpGet("my")]
    [Authorize(Roles = "PortfolioDirector")]
    public async Task<IActionResult> GetMyPortfolios()
    {
        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        if (user == null) return NotFound();

        var portfolios = await _db.Portfolios
            .Include(p => p.Projects)
            .Where(p => p.PortfolioDirectorId == user.Id)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Description,
                p.CreatedAt,
                projectCount = p.Projects.Count
            })
            .ToListAsync();

        return Ok(portfolios);
    }
    // Lister tous les PortfolioDirectors disponibles
    [HttpGet("directors")]
    [Authorize(Roles = "HeadOfCDS")]
    public async Task<IActionResult> GetDirectors()
    {
        var directors = await _db.Users
            .Where(u => u.Role == GlobalRole.PortfolioDirector)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                portfolioCount = _db.Portfolios
                    .Count(p => p.PortfolioDirectorId == u.Id)
            })
            .ToListAsync();

        return Ok(directors);
    }
}

public class CreatePortfolioRequest
{
    [Required(ErrorMessage = "Name est obligatoire")]
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    [Required(ErrorMessage = "PortfolioDirectorId est obligatoire")]
    public Guid PortfolioDirectorId { get; set; }
}

public class AssignDirectorRequest
{
    [Required]
    public Guid PortfolioDirectorId { get; set; }
}