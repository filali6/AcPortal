using Backend.Modules.Git.Models;

namespace Backend.Modules.Git.Services;
public interface IGitProvider
{
    Task<string> CreateRepoAsync(Guid streamId, string streamName, Guid projectId);
    Task<string> PushFileAsync(Guid streamId, string stepName, string toolName, string fileName, string content);
    Task<List<GitFileDto>> GetFilesAsync(Guid streamId);
    Task<string> GetRepoUrlAsync(Guid streamId);
    Task CreateTagAsync(Guid streamId, string tag, string message);
}