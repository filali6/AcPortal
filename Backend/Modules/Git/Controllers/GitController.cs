using Backend.Modules.Git.Services;
using Backend.Modules.Projects.Models;
using Backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Git.Controllers;

[ApiController]
[Route("api/git")]
public class GitController : ControllerBase
{
    private readonly GitService _gitService;
    private readonly AppDbContext _db;

    public GitController(GitService gitService, AppDbContext db)
    {
        _gitService = gitService;
        _db = db;
    }

   
    [HttpPost("steps/{stepId}/config")]
    public async Task<IActionResult> UploadConfig(
        Guid stepId,
        IFormFile file)
    {
        var step = await _db.ProjectSteps
            .Include(s => s.Stream)
            .FirstOrDefaultAsync(s => s.Id == stepId);

        if (step is null)
            return NotFound("Step not found");

        if (file is null || file.Length == 0)
            return BadRequest("No file provided");

        // Lit le contenu du fichier
        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync();

        // Calcule la version (nombre de fichiers existants + 1)
        var version = await _db.StepConfigFiles
            .CountAsync(f => f.StepId == stepId) + 1;

        // Push vers GitHub
        await _gitService.PushConfigFileAsync(stepId, file.FileName, content);

        // Sauvegarde en DB
        var configFile = new StepConfigFile
        {
            StepId = stepId,
            FileName = file.FileName,
            CommitHash = step.LastCommitHash ?? string.Empty,
            Version = version,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = User.Identity?.Name ?? "unknown"
        };

        _db.StepConfigFiles.Add(configFile);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "File uploaded successfully",
            fileName = file.FileName,
            commitHash = step.LastCommitHash,
            version,
            repoUrl = await _gitService.GetRepoUrlAsync(step.Stream!.Id)
        });
    }

    // Génère un fichier mock selon l'outil du step
    [HttpPost("steps/{stepId}/config/mock")]
    public async Task<IActionResult> GenerateMock(Guid stepId)
    {
        var step = await _db.ProjectSteps
            .Include(s => s.Stream)
            .FirstOrDefaultAsync(s => s.Id == stepId);

        if (step is null)
            return NotFound("Step not found");

        // Génère le contenu mock selon l'outil
        var (fileName, content) = GenerateMockContent(step.ToolName);

        var version = await _db.StepConfigFiles
            .CountAsync(f => f.StepId == stepId) + 1;

        await _gitService.PushConfigFileAsync(stepId, fileName, content);

        var configFile = new StepConfigFile
        {
            StepId = stepId,
            FileName = fileName,
            CommitHash = step.LastCommitHash ?? string.Empty,
            Version = version,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = "mock-generator"
        };

        _db.StepConfigFiles.Add(configFile);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Mock config generated successfully",
            fileName,
            content,
            commitHash = step.LastCommitHash,
            version,
            repoUrl = await _gitService.GetRepoUrlAsync(step.Stream!.Id)
        });
    }

    // Liste tous les fichiers du repo d'un stream
    [HttpGet("streams/{streamId}/configs")]
    public async Task<IActionResult> GetStreamConfigs(Guid streamId)
    {
        // Fichiers depuis GitHub
        var gitFiles = await _gitService.GetStreamFilesAsync(streamId);

        // Fichiers depuis la DB (métadonnées)
        var dbFiles = await _db.StepConfigFiles
            .Include(f => f.Step)
            .Where(f => f.Step.StreamId == streamId)
            .OrderBy(f => f.Step.Order)
            .ToListAsync();

        return Ok(new
        {
            repoUrl = await _gitService.GetRepoUrlAsync(streamId),
            gitFiles,
            dbFiles = dbFiles.Select(f => new
            {
                f.Id,
                f.FileName,
                f.CommitHash,
                f.Version,
                f.UploadedAt,
                f.UploadedBy,
                stepName = f.Step.StepName,
                toolName = f.Step.ToolName
            })
        });
    }

     
    [HttpPut("streams/{streamId}/validate")]
    public async Task<IActionResult> ValidateStream(Guid streamId)
    {
        var stream = await _db.Streams.FindAsync(streamId);
        if (stream is null)
            return NotFound("Stream not found");

       
        var tagName = $"v1.0";

        await _gitService.TagStreamVersionAsync(streamId, tagName);

        return Ok(new
        {
            message = $"Stream validated — tag {tagName} created",
            repoUrl = stream.GitRepoUrl,
            tag = tagName
        });
    }

    
    private static (string fileName, string content) GenerateMockContent(string toolName)
    {
        return toolName.ToLower() switch
        {
            "axeiam" => ("iam-config.json", """
                {
                  "roles": [
                    { "name": "credit-analyst", "permissions": ["read:loans", "write:analysis"] },
                    { "name": "credit-manager", "permissions": ["read:loans", "approve:loans"] },
                    { "name": "risk-officer", "permissions": ["read:all", "write:risk-reports"] }
                  ],
                  "policies": [
                    { "resource": "loan-applications", "action": "read", "roles": ["credit-analyst", "credit-manager"] },
                    { "resource": "loan-applications", "action": "approve", "roles": ["credit-manager"] }
                  ]
                }
                """),

            "axebpm" => ("bpm-workflow.json", """
                {
                  "workflow": {
                    "name": "loan-origination-process",
                    "steps": [
                      { "id": "application-submitted", "type": "start", "next": "kyc-check" },
                      { "id": "kyc-check", "type": "task", "assignee": "credit-analyst", "next": "credit-scoring" },
                      { "id": "credit-scoring", "type": "automated", "next": "underwriting" },
                      { "id": "underwriting", "type": "task", "assignee": "credit-manager", "next": "approval" },
                      { "id": "approval", "type": "decision", "next": ["approved", "rejected"] },
                      { "id": "approved", "type": "end" },
                      { "id": "rejected", "type": "end" }
                    ]
                  }
                }
                """),

            "axegui" => ("gui-forms.json", """
                {
                  "forms": [
                    {
                      "id": "loan-application-form",
                      "title": "Loan Application",
                      "fields": [
                        { "id": "client-name", "type": "text", "label": "Client Name", "required": true },
                        { "id": "loan-amount", "type": "number", "label": "Loan Amount", "required": true },
                        { "id": "loan-duration", "type": "select", "label": "Duration", "options": ["12", "24", "36", "60"] },
                        { "id": "loan-purpose", "type": "textarea", "label": "Purpose", "required": false }
                      ]
                    }
                  ]
                }
                """),

            _ => ("config.json", """
                {
                  "tool": "unknown",
                  "config": {},
                  "generatedAt": "mock",
                  "note": "Replace with actual tool configuration"
                }
                """)
        };
    }
}