using System.Text;
using Backend.Data;
using Backend.Modules.Git.Controllers;
using Backend.Modules.Git.Models;
using Backend.Modules.Git.Services;
using Backend.Modules.Projects.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Stream = Backend.Modules.Projects.Models.Stream;

namespace Backend.Tests.Git;

public class GitControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IGitProvider> _providerMock = new();

    public GitControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private GitController CreateController()
    {
        var gitService = new GitService(_providerMock.Object, _db, NullLogger<GitService>.Instance);
        var controller = new GitController(gitService, _db);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static IFormFile CreateFormFile(string content = "config content", string fileName = "config.json")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName);
    }

    private async Task<(Stream stream, ProjectStep step)> SeedStreamAndStep()
    {
        var stream = new Stream { Name = "Stream 1", GitRepoUrl = "http://repo" };
        var step = new ProjectStep { StreamId = stream.Id, Stream = stream, StepName = "Step1", ToolName = "AxeIAM" };
        _db.Streams.Add(stream);
        _db.ProjectSteps.Add(step);
        await _db.SaveChangesAsync();
        return (stream, step);
    }

    [Fact]
    public async Task UploadConfig_WhenStepNotFound_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.UploadConfig(Guid.NewGuid(), CreateFormFile());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UploadConfig_WhenFileMissing_ReturnsBadRequest()
    {
        var (_, step) = await SeedStreamAndStep();
        var controller = CreateController();

        var result = await controller.UploadConfig(step.Id, null!);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UploadConfig_WithValidFile_PushesAndPersistsConfigFile()
    {
        var (_, step) = await SeedStreamAndStep();
        _providerMock.Setup(p => p.PushFileAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("commit-hash");
        _providerMock.Setup(p => p.GetRepoUrlAsync(It.IsAny<Guid>())).ReturnsAsync("http://repo");
        var controller = CreateController();

        var result = await controller.UploadConfig(step.Id, CreateFormFile());

        result.Should().BeOfType<OkObjectResult>();
        (await _db.StepConfigFiles.SingleAsync()).StepId.Should().Be(step.Id);
    }

    [Fact]
    public async Task GenerateMock_WhenStepNotFound_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.GenerateMock(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GenerateMock_WithKnownTool_GeneratesConfigFile()
    {
        var (_, step) = await SeedStreamAndStep();
        _providerMock.Setup(p => p.PushFileAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("commit-hash");
        _providerMock.Setup(p => p.GetRepoUrlAsync(It.IsAny<Guid>())).ReturnsAsync("http://repo");
        var controller = CreateController();

        var result = await controller.GenerateMock(step.Id);

        result.Should().BeOfType<OkObjectResult>();
        (await _db.StepConfigFiles.SingleAsync()).UploadedBy.Should().Be("mock-generator");
    }

    [Fact]
    public async Task GetStreamConfigs_ReturnsGitAndDbFiles()
    {
        var (stream, step) = await SeedStreamAndStep();
        _db.StepConfigFiles.Add(new StepConfigFile { StepId = step.Id, FileName = "f.json", CommitHash = "abc" });
        await _db.SaveChangesAsync();
        _providerMock.Setup(p => p.GetFilesAsync(stream.Id)).ReturnsAsync(new List<GitFileDto>());
        _providerMock.Setup(p => p.GetRepoUrlAsync(stream.Id)).ReturnsAsync("http://repo");
        var controller = CreateController();

        var result = await controller.GetStreamConfigs(stream.Id);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ValidateStream_WhenStreamNotFound_ReturnsNotFound()
    {
        var controller = CreateController();

        var result = await controller.ValidateStream(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ValidateStream_WhenStreamExists_CreatesTag()
    {
        var (stream, _) = await SeedStreamAndStep();
        var controller = CreateController();

        var result = await controller.ValidateStream(stream.Id);

        result.Should().BeOfType<OkObjectResult>();
        _providerMock.Verify(p => p.CreateTagAsync(stream.Id, "v1.0", It.IsAny<string>()), Times.Once);
    }
}
