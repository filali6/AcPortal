using Backend.Modules.Git.Models;
using Backend.Modules.Projects.Models;
using Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Git.Services;

public class GitService
{
    private readonly IGitProvider _gitProvider;
    private readonly AppDbContext _db;
    private readonly ILogger<GitService> _logger;

    public GitService(IGitProvider gitProvider, AppDbContext db, ILogger<GitService> logger)
    {
        _gitProvider = gitProvider;
        _db = db;
        _logger = logger;
    }

  
    public async Task InitStreamRepoAsync(Guid streamId, Guid projectId)
    {
        var stream = await _db.Streams.FindAsync(streamId);
        if (stream is null)
        {
            _logger.LogWarning("Stream {Id} not found", streamId);
            return;
        }

        try
        {
            var repoUrl = await _gitProvider.CreateRepoAsync(streamId, stream.Name, projectId);

            // Sauvegarde l'URL du repo sur le stream
            stream.GitRepoUrl = repoUrl;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Git repo created for stream {Id} : {Url}", streamId, repoUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create git repo for stream {Id}", streamId);
        }
    }

    // Appelé quand un consultant uploade un fichier de config
    public async Task PushConfigFileAsync(Guid stepId, string fileName, string content)
    {
        var step = await _db.ProjectSteps
            .Include(s => s.Stream)
            .FirstOrDefaultAsync(s => s.Id == stepId);

        if (step is null)
        {
            _logger.LogWarning("Step {Id} not found", stepId);
            return;
        }

        try
        {
            var commitHash = await _gitProvider.PushFileAsync(
                step.Stream!.Id,
                step.StepName,    
                step.ToolName,    
                fileName,
                content
            );

            // Sauvegarde le commit hash sur le step
            step.LastCommitHash = commitHash;
            step.LastCommitAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation("File {File} pushed for step {Id} — commit {Hash}",
                fileName, stepId, commitHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push file for step {Id}", stepId);
            throw;
        }
    }

    // Appelé pour afficher les configs d'un stream
    public async Task<List<GitFileDto>> GetStreamFilesAsync(Guid streamId)
    {
        try
        {
            return await _gitProvider.GetFilesAsync(streamId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get files for stream {Id}", streamId);
            return new List<GitFileDto>();
        }
    }

    // Appelé pour récupérer l'URL du repo d'un stream
    public async Task<string> GetRepoUrlAsync(Guid streamId)
    {
        return await _gitProvider.GetRepoUrlAsync(streamId);
    }

    // Appelé quand le Team Lead valide le stream
    public async Task TagStreamVersionAsync(Guid streamId, string version)
    {
        try
        {
            await _gitProvider.CreateTagAsync(
                streamId,
                version,
                $"Version {version} validated by Team Lead"
            );

            _logger.LogInformation("Tag {Version} created for stream {Id}", version, streamId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create tag for stream {Id}", streamId);
            throw;
        }
    }
}