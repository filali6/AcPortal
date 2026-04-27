using Backend.Data;
using Backend.Modules.Projects.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Projects.Controllers;

[ApiController]
[Route("api/portfolios")]
public class PortfolioController : ControllerBase
{
    private readonly AppDbContext _db;

    public PortfolioController(AppDbContext db)
    {
        _db = db;
    }

    // HEAD OF CDS — créer un portfolio
    [HttpPost]
    [Authorize(Roles = "HeadOfCDS")]
    public async Task<IActionResult> Create([FromBody] Portfolio dto)
    {
        var portfolio = new Portfolio
        {
            Name = dto.Name,
            Description = dto.Description
        };
        _db.Portfolios.Add(portfolio);
        await _db.SaveChangesAsync();
        return Ok(portfolio);
    }

    // GET tous les portfolios
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll()
    {
        var portfolios = await _db.Portfolios.ToListAsync();
        return Ok(portfolios);
    }
}