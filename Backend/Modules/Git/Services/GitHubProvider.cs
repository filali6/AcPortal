using System.Text;
using Backend.Modules.Git.Models;
using Microsoft.Extensions.Configuration;
using Octokit;

namespace Backend.Modules.Git.Services;

public class GitHubProvider : IGitProvider
{
    private readonly GitHubClient _client;
    private readonly string _organization;
    private readonly string _repoPrefix;

    public GitHubProvider(IConfiguration configuration)
    {
        var token = configuration["Git:GitHub:Token"]!;
        _organization = configuration["Git:GitHub:Organization"]!;
        _repoPrefix = configuration["Git:GitHub:RepoPrefix"] ?? "acportal-stream";

        _client = new GitHubClient(new ProductHeaderValue("ACPortal"))
        {
            Credentials = new Credentials(token)
        };
    }

    private string GetRepoName(Guid streamId) => $"{_repoPrefix}-{streamId}";

    public async Task<string> CreateRepoAsync(Guid streamId, string streamName, Guid projectId)
    {
        var repoName = GetRepoName(streamId);

        var newRepo = new NewRepository(repoName)
        {
            Description = $"ACPortal stream: {streamName}",
            Private = true,
            AutoInit = true
        };

        try
        {
            Repository repo;
            try
            {
                repo = await _client.Repository.Create(_organization, newRepo);
            }
            catch
            {
                repo = await _client.Repository.Create(newRepo);
            }

            await _client.Repository.ReplaceAllTopics(
    _organization,
    repoName,
    new RepositoryTopics(new List<string> { $"project-{projectId}", "acportal" })
);

            return repo.HtmlUrl;
        }
        catch (RepositoryExistsException)
        {
            return await GetRepoUrlAsync(streamId);
        }
    }

    public async Task<string> PushFileAsync(Guid streamId, string stepName, string toolName, string fileName, string content)
    {
        var repoName = GetRepoName(streamId);
        var filePath = $"{stepName}-{toolName}/{fileName}";

        try
        {
            try
            {
                var existingFile = await _client.Repository.Content.GetAllContents(
                    _organization, repoName, filePath);

                var updateRequest = new UpdateFileRequest(
                    $"update: {fileName} for {stepName}",
                    content,
                    existingFile[0].Sha
                );

                var result = await _client.Repository.Content.UpdateFile(
                    _organization, repoName, filePath, updateRequest);

                return result.Commit.Sha;
            }
            catch (NotFoundException)
            {
                var createRequest = new CreateFileRequest(
                    $"feat: add {fileName} for {stepName}",
                    content
                );

                var result = await _client.Repository.Content.CreateFile(
                    _organization, repoName, filePath, createRequest);

                return result.Commit.Sha;
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to push file to GitHub: {ex.Message}");
        }
    }

    public async Task<List<GitFileDto>> GetFilesAsync(Guid streamId)
    {
        var repoName = GetRepoName(streamId);
        var files = new List<GitFileDto>();

        try
        {
            var contents = await _client.Repository.Content.GetAllContents(
                _organization, repoName);

            foreach (var item in contents.Where(c => c.Type == ContentType.Dir))
            {
                var dirContents = await _client.Repository.Content.GetAllContents(
                    _organization, repoName, item.Path);

                foreach (var file in dirContents.Where(f => f.Type == ContentType.File))
                {
                    var parts = item.Name.Split('-');
                    files.Add(new GitFileDto
                    {
                        FileName = file.Name,
                        StepName = parts.Length > 0 ? parts[0] : item.Name,
                        ToolName = parts.Length > 1 ? parts[1] : "",
                        DownloadUrl = file.DownloadUrl,
                        CommitHash = file.Sha
                    });
                }
            }
        }
        catch (NotFoundException) { }

        return files;
    }

    public async Task<string> GetRepoUrlAsync(Guid streamId)
    {
        var repoName = GetRepoName(streamId);
        try
        {
            var repo = await _client.Repository.Get(_organization, repoName);
            return repo.HtmlUrl;
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task CreateTagAsync(Guid streamId, string tag, string message)
    {
        var repoName = GetRepoName(streamId);

        var commits = await _client.Repository.Commit.GetAll(_organization, repoName);
        var latestSha = commits[0].Sha;

        var newTag = new NewTag
        {
            Tag = tag,
            Message = message,
            Object = latestSha,
            Type = TaggedType.Commit
        };

        await _client.Git.Tag.Create(_organization, repoName, newTag);
    }
}