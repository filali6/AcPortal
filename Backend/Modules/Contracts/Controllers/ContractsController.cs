using Backend.Data;
using Backend.Modules.Contracts.Models;
using Backend.Modules.Contracts.Services;
using Backend.Modules.Events.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Contracts.Controllers;

[ApiController]
[Route("api/contracts")]
[Authorize]
public class ContractsController : ControllerBase
{
    private readonly ContractsService _contractsService;
    private readonly AppDbContext _db;
    private readonly EventPublisher _eventPublisher;
    private readonly IWebHostEnvironment _env;

    public ContractsController(
        ContractsService contractsService,
        AppDbContext db,
        EventPublisher eventPublisher,
        IWebHostEnvironment env)
    {
        _contractsService = contractsService;
        _db = db;
        _eventPublisher = eventPublisher;
        _env = env;
    }

    // POST /api/contracts
    [HttpPost]
    public async Task<IActionResult> Create([FromForm] CreateContractRequest request)
    {
        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        if (user == null) return NotFound();

        var files = request.Files ?? new List<IFormFile>();
        var contract = await _contractsService.CreateAsync(
            request.ClientName,
            request.Description,
            user.Id,
            files
        );

        // Publier l'event vers HeadOfCDS
        await _eventPublisher.PublishAsync(new
        {
            eventType = "ContratSigné",
            clientName = contract.ClientName,
            contractId = contract.Id,
            description = contract.Description
        });

        return Ok(contract);
    }

    // GET /api/contracts/my
    [HttpGet("my")]
    public async Task<IActionResult> GetMy()
    {
        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        if (user == null) return NotFound();

        var contracts = await _contractsService.GetMyContractsAsync(user.Id);
        var stats = await _contractsService.GetStatsAsync(user.Id);

        return Ok(new
        {
            stats = new
            {
                total = stats.total,
                projectCreated = stats.projectCreated,
                pending = stats.pending
            },
            contracts = contracts.Select(c => new
            {
                c.Id,
                c.ClientName,
                c.Description,
                c.Status,
                c.CreatedAt,
                c.ProjectId,
                c.FilesPaths,
                projectCreatedAt = c.ProjectId.HasValue
        ? _db.Projects
            .Where(p => p.Id == c.ProjectId)
            .Select(p => (DateTime?)p.CreatedAt)
            .FirstOrDefault()
        : null
            })
        });
    }

    // GET /api/contracts/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var contract = await _contractsService.GetByIdAsync(id);
        if (contract == null) return NotFound();
        return Ok(contract);
    }

    // PATCH /api/contracts/{id}/files
    [HttpPatch("{id:guid}/files")]
    public async Task<IActionResult> AddFiles(Guid id, [FromForm] AddFilesRequest request)
    {
        var files = request.Files ?? new List<IFormFile>();
        var contract = await _contractsService.AddFilesAsync(id, files, _env);
        if (contract == null) return NotFound();
        return Ok(contract);
    }

    // GET /api/contracts/files/{fileName}
    [HttpGet("files/{fileName}")]
    public IActionResult DownloadFile(string fileName)
    {
        var filePath = Path.Combine(_env.ContentRootPath, "uploads", fileName);
        if (!System.IO.File.Exists(filePath)) return NotFound();

        var fileBytes = System.IO.File.ReadAllBytes(filePath);
        var contentType = "application/octet-stream";
        return File(fileBytes, contentType, fileName);
    }

    // PUT /api/contracts/{id}
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromForm] UpdateContractRequest request)
    {
        var contract = await _contractsService.UpdateAsync(
            id,
            request.ClientName,
            request.Description,
            request.Status,
            request.NewFiles
        );
        if (contract == null) return NotFound();
        return Ok(contract);
    }

    // DELETE /api/contracts/{id}/files/{fileName}
    [HttpDelete("{id:guid}/files/{fileName}")]
    public async Task<IActionResult> DeleteFile(Guid id, string fileName)
    {
        fileName = Uri.UnescapeDataString(fileName);
        var contract = await _contractsService.DeleteFileAsync(id, fileName);
        if (contract == null) return NotFound();
        return Ok(contract);
    }
    [HttpGet("all")]
    [Authorize(Roles = "HeadOfCDS")]
    public async Task<IActionResult> GetAll()
    {
        var contracts = await _db.Contracts
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.ClientName,
                c.Description,
                c.Status,
                c.CreatedAt,
                c.ProjectId,
                c.FilesPaths
            })
            .ToListAsync();
        return Ok(contracts);
    }

}

public class CreateContractRequest
{
    public string ClientName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<IFormFile>? Files { get; set; }
}

public class AddFilesRequest
{
    public List<IFormFile>? Files { get; set; }
}

public class UpdateContractRequest
{
    public string ClientName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Status { get; set; }
    public List<IFormFile>? NewFiles { get; set; }
}