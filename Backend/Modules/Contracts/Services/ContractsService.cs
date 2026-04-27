using Backend.Data;
using Backend.Modules.Contracts.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Contracts.Services;

public class ContractsService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ContractsService> _logger;
    private readonly IWebHostEnvironment _env;

    public ContractsService(AppDbContext db, ILogger<ContractsService> logger, IWebHostEnvironment env)
    {
        _db = db;
        _logger = logger;
        _env = env;
    }

    public async Task<Contract> CreateAsync(string clientName, string description, Guid dafUserId, List<IFormFile> files)
    {
        var contract = new Contract
        {
            ClientName = clientName,
            Description = description,
            DafUserId = dafUserId
        };

        // Sauvegarde des fichiers
        var uploadsPath = Path.Combine(_env.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadsPath);

        foreach (var file in files)
        {
            if (file.Length > 0)
            {
                var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                var filePath = Path.Combine(uploadsPath, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);
                contract.FilesPaths.Add(fileName);
            }
        }

        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync();
        return contract;
    }

    public async Task<List<Contract>> GetMyContractsAsync(Guid dafUserId)
    {
        return await _db.Contracts
            .Where(c => c.DafUserId == dafUserId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<Contract?> GetByIdAsync(Guid id)
    {
        return await _db.Contracts.FindAsync(id);
    }

    public async Task<Contract?> AddFilesAsync(Guid contractId, List<IFormFile> files, IWebHostEnvironment env)
    {
        var contract = await _db.Contracts.FindAsync(contractId);
        if (contract == null) return null;

        var uploadsPath = Path.Combine(env.ContentRootPath, "uploads");
        Directory.CreateDirectory(uploadsPath);

        foreach (var file in files)
        {
            if (file.Length > 0)
            {
                var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                var filePath = Path.Combine(uploadsPath, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);
                contract.FilesPaths.Add(fileName);
            }
        }

        await _db.SaveChangesAsync();
        return contract;
    }

    public async Task LinkProjectAsync(Guid contractId, Guid projectId)
    {
        var contract = await _db.Contracts.FindAsync(contractId);
        if (contract == null) return;
        contract.ProjectId = projectId;
        contract.Status = ContractStatus.ProjectCreated;
        await _db.SaveChangesAsync();
    }

    public async Task<(int total, int projectCreated, int pending)> GetStatsAsync(Guid dafUserId)
    {
        var contracts = await _db.Contracts.Where(c => c.DafUserId == dafUserId).ToListAsync();
        return (
            contracts.Count,
            contracts.Count(c => c.ProjectId.HasValue),
            contracts.Count(c => !c.ProjectId.HasValue)
        );
    }
}