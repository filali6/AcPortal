using Backend.Data;
using Backend.Modules.Git.Models;
using Backend.Modules.Git.Services;
using Backend.Modules.Projects.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ProjectStream = Backend.Modules.Projects.Models.Stream;

namespace Backend.Tests.Git;

public class GitServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IGitProvider> _gitProvider;
    private readonly GitService _service;

    public GitServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _gitProvider = new Mock<IGitProvider>();
        _service = new GitService(_gitProvider.Object, _db, NullLogger<GitService>.Instance);
    }

   public void Dispose()
{
    _db.Dispose();
    GC.SuppressFinalize(this);
}

    [Fact]
    public async Task InitStreamRepoAsync_StreamNotFound_DoesNotCallProvider()
    {
        await _service.InitStreamRepoAsync(Guid.NewGuid(), Guid.NewGuid());

        _gitProvider.Verify(p => p.CreateRepoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task InitStreamRepoAsync_Success_SavesRepoUrlOnStream()
    {
        var stream = new ProjectStream { Name = "S1", ProjectId = Guid.NewGuid() };
        _db.Streams.Add(stream);
        await _db.SaveChangesAsync();

        _gitProvider
            .Setup(p => p.CreateRepoAsync(stream.Id, stream.Name, It.IsAny<Guid>()))
            .ReturnsAsync("https://git.example.com/s1.git");

        await _service.InitStreamRepoAsync(stream.Id, Guid.NewGuid());

        var updated = await _db.Streams.FindAsync(stream.Id);
        updated!.GitRepoUrl.Should().Be("https://git.example.com/s1.git");
    }

    [Fact]
    public async Task InitStreamRepoAsync_ProviderThrows_DoesNotThrow_AndLeavesRepoUrlUnset()
    {
        var stream = new ProjectStream { Name = "S1", ProjectId = Guid.NewGuid() };
        _db.Streams.Add(stream);
        await _db.SaveChangesAsync();

        _gitProvider
            .Setup(p => p.CreateRepoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var act = async () => await _service.InitStreamRepoAsync(stream.Id, Guid.NewGuid());

        await act.Should().NotThrowAsync();
        var updated = await _db.Streams.FindAsync(stream.Id);
        updated!.GitRepoUrl.Should().BeNull();
    }

    [Fact]
    public async Task PushConfigFileAsync_StepNotFound_DoesNotCallProvider()
    {
        await _service.PushConfigFileAsync(Guid.NewGuid(), "file.yaml", "content");

        _gitProvider.Verify(
            p => p.PushFileAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task PushConfigFileAsync_Success_SavesCommitHashAndTimestamp()
    {
        var stream = new ProjectStream { Name = "S1", ProjectId = Guid.NewGuid() };
        _db.Streams.Add(stream);
        var step = new ProjectStep
        {
            ProjectId = Guid.NewGuid(),
            StreamId = stream.Id,
            StepName = "Step1",
            ToolName = "Tool1"
        };
        _db.ProjectSteps.Add(step);
        await _db.SaveChangesAsync();

        _gitProvider
            .Setup(p => p.PushFileAsync(stream.Id, step.StepName, step.ToolName, "file.yaml", "content"))
            .ReturnsAsync("abc123");

        await _service.PushConfigFileAsync(step.Id, "file.yaml", "content");

        var updated = await _db.ProjectSteps.FindAsync(step.Id);
        updated!.LastCommitHash.Should().Be("abc123");
        updated.LastCommitAt.Should().NotBeNull();
    }

    [Fact]
    public async Task PushConfigFileAsync_ProviderThrows_RethrowsException()
    {
        var stream = new ProjectStream { Name = "S1", ProjectId = Guid.NewGuid() };
        _db.Streams.Add(stream);
        var step = new ProjectStep
        {
            ProjectId = Guid.NewGuid(),
            StreamId = stream.Id,
            StepName = "Step1",
            ToolName = "Tool1"
        };
        _db.ProjectSteps.Add(step);
        await _db.SaveChangesAsync();

        _gitProvider
            .Setup(p => p.PushFileAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("push failed"));

        var act = async () => await _service.PushConfigFileAsync(step.Id, "file.yaml", "content");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("push failed");
    }

    [Fact]
    public async Task GetStreamFilesAsync_ProviderThrows_ReturnsEmptyList()
    {
        _gitProvider.Setup(p => p.GetFilesAsync(It.IsAny<Guid>())).ThrowsAsync(new Exception("fail"));

        var result = await _service.GetStreamFilesAsync(Guid.NewGuid());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStreamFilesAsync_Success_ReturnsProviderFiles()
    {
        var streamId = Guid.NewGuid();
        var files = new List<GitFileDto>
        {
            new()
            {
                FileName = "a.yaml",
                StepName = "Step1",
                ToolName = "Tool1",
                CommitHash = "h1",
                DownloadUrl = "url1",
                LastModified = DateTime.UtcNow
            }
        };
        _gitProvider.Setup(p => p.GetFilesAsync(streamId)).ReturnsAsync(files);

        var result = await _service.GetStreamFilesAsync(streamId);

        result.Should().BeEquivalentTo(files);
    }

    [Fact]
    public async Task GetRepoUrlAsync_ReturnsProviderResult()
    {
        var streamId = Guid.NewGuid();
        _gitProvider.Setup(p => p.GetRepoUrlAsync(streamId)).ReturnsAsync("https://git.example.com/repo.git");

        var result = await _service.GetRepoUrlAsync(streamId);

        result.Should().Be("https://git.example.com/repo.git");
    }

    [Fact]
    public async Task TagStreamVersionAsync_Success_CallsProviderWithFormattedMessage()
    {
        var streamId = Guid.NewGuid();

        await _service.TagStreamVersionAsync(streamId, "v1.0");

        _gitProvider.Verify(p => p.CreateTagAsync(streamId, "v1.0", "Version v1.0 validated by Team Lead"), Times.Once);
    }

    [Fact]
    public async Task TagStreamVersionAsync_ProviderThrows_RethrowsException()
    {
        _gitProvider
            .Setup(p => p.CreateTagAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("tag failed"));

        var act = async () => await _service.TagStreamVersionAsync(Guid.NewGuid(), "v1.0");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("tag failed");
    }
}
