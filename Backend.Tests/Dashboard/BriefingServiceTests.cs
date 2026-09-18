using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Contracts.Services;
using Backend.Modules.Dashboard.Services;
using Backend.Modules.Projects.Models;
using Backend.Modules.Tasks.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace Backend.Tests.Dashboard;

public class BriefingServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IContractSummaryService> _summaryService;
    private readonly IMemoryCache _cache;

    public BriefingServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _summaryService = new Mock<IContractSummaryService>();
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    public void Dispose()
    {
        _db.Dispose();
        _cache.Dispose();
    }

    private BriefingService CreateService() => new(_db, _summaryService.Object, _cache);

    [Fact]
    public async Task GenerateBriefingAsync_ReturnsCachedValue_WithoutCallingSummarizer()
    {
        var userId = Guid.NewGuid();
        _cache.Set($"briefing:{userId}", "cached briefing");
        var service = CreateService();

        var result = await service.GenerateBriefingAsync(userId, "kc-1", "Alice", "Consultant");

        result.Should().Be("cached briefing");
        _summaryService.Verify(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GenerateBriefingAsync_ProjectManager_BuildsCorrectContext_AndCachesResult()
    {
        var pmId = Guid.NewGuid();
        _db.Projects.Add(new Project { Name = "P1", ProjectManagerId = pmId });
        _db.Projects.Add(new Project { Name = "P2", ProjectManagerId = pmId });
        _db.AcpTasks.Add(new AcpTask { Title = "T1", AssignedTo = "kc-pm", Status = AcpTaskStatus.Pending });
        _db.AcpTasks.Add(new AcpTask { Title = "T2", AssignedTo = "kc-pm", Status = AcpTaskStatus.Done });
        await _db.SaveChangesAsync();

        string? capturedContext = null;
        _summaryService
            .Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((context, _) => capturedContext = context)
            .ReturnsAsync("Summarized briefing");

        var service = CreateService();

        var result = await service.GenerateBriefingAsync(pmId, "kc-pm", "Alice", "ProjectManager");

        result.Should().Be("Summarized briefing");
        capturedContext.Should().Contain("Projects managed: 2");
        capturedContext.Should().Contain("Pending tasks: 1");
        capturedContext.Should().Contain("Completed tasks: 1");

        // Second call should be served from cache, summarizer not called again.
        var second = await service.GenerateBriefingAsync(pmId, "kc-pm", "Alice", "ProjectManager");
        second.Should().Be("Summarized briefing");
        _summaryService.Verify(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task GenerateBriefingAsync_HeadOfCDS_BuildsCorrectContext()
    {
        _db.Projects.Add(new Project { Name = "P1", ProjectManagerId = Guid.NewGuid() });
        _db.Projects.Add(new Project { Name = "P2" }); // no PM
        _db.AcpTasks.Add(new AcpTask { Title = "T1", Status = AcpTaskStatus.Pending });
        _db.AcpTasks.Add(new AcpTask { Title = "T2", AssignedTo = "kc-head", Status = AcpTaskStatus.Pending });
        _db.AcpTasks.Add(new AcpTask { Title = "T3", Status = AcpTaskStatus.Done });
        await _db.SaveChangesAsync();

        string? capturedContext = null;
        _summaryService
            .Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((context, _) => capturedContext = context)
            .ReturnsAsync("ok");

        var service = CreateService();
        await service.GenerateBriefingAsync(Guid.NewGuid(), "kc-head", "Bob", "HeadOfCDS");

        capturedContext.Should().Contain("Total projects: 2");
        capturedContext.Should().Contain("Projects without PM: 1");
        capturedContext.Should().Contain("Pending tasks across all projects: 2");
        capturedContext.Should().Contain("Completed tasks: 1");
        capturedContext.Should().Contain("My own personal pending tasks: 1");
    }

    [Fact]
    public async Task GenerateBriefingAsync_Consultant_BuildsCorrectContext()
    {
        var user = new User { FullName = "Cons", Email = "c@test.com", KeycloakId = "kc-cons" };
        _db.Users.Add(user);
        var stream = new Backend.Modules.Projects.Models.Stream { Name = "Stream1" };
        _db.Streams.Add(stream);
        await _db.SaveChangesAsync();
        _db.StreamMembers.Add(new StreamMember { StreamId = stream.Id, ConsultantId = user.Id, TeamType = TeamType.Business });
        _db.AcpTasks.Add(new AcpTask { Title = "T1", AssignedTo = "kc-cons", Status = AcpTaskStatus.Pending });
        await _db.SaveChangesAsync();

        string? capturedContext = null;
        _summaryService
            .Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((context, _) => capturedContext = context)
            .ReturnsAsync("ok");

        var service = CreateService();
        await service.GenerateBriefingAsync(Guid.NewGuid(), "kc-cons", "Cons", "Consultant");

        capturedContext.Should().Contain("Active streams: 1");
        capturedContext.Should().Contain("Pending tasks: 1");
        capturedContext.Should().Contain("Completed tasks: 0");
    }

    [Fact]
    public async Task GenerateBriefingAsync_TeamLead_BuildsCorrectContext()
    {
        _db.AcpTasks.Add(new AcpTask { Title = "T1", AssignedTo = "kc-lead", Status = AcpTaskStatus.Pending });
        _db.AcpTasks.Add(new AcpTask { Title = "T2", AssignedTo = "kc-lead", Status = AcpTaskStatus.Done });
        await _db.SaveChangesAsync();

        string? capturedContext = null;
        _summaryService
            .Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((context, _) => capturedContext = context)
            .ReturnsAsync("ok");

        var service = CreateService();
        await service.GenerateBriefingAsync(Guid.NewGuid(), "kc-lead", "Lead", "BusinessTeamLead");

        capturedContext.Should().Contain("Role: BusinessTeamLead");
        capturedContext.Should().Contain("Pending tasks: 1");
        capturedContext.Should().Contain("Completed tasks: 1");
    }

    [Fact]
    public async Task GenerateBriefingAsync_PortfolioDirector_BuildsCorrectContext()
    {
        var directorId = Guid.NewGuid();
        var portfolio = new Portfolio { Name = "Portfolio1", PortfolioDirectorId = directorId };
        _db.Portfolios.Add(portfolio);
        await _db.SaveChangesAsync();
        _db.Projects.Add(new Project { Name = "P1", PortfolioId = portfolio.Id });
        _db.Projects.Add(new Project { Name = "P2", PortfolioId = portfolio.Id, ProjectManagerId = Guid.NewGuid() });
        _db.AcpTasks.Add(new AcpTask { Title = "T1", AssignedTo = "kc-dir", Status = AcpTaskStatus.Pending });
        await _db.SaveChangesAsync();

        string? capturedContext = null;
        _summaryService
            .Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((context, _) => capturedContext = context)
            .ReturnsAsync("ok");

        var service = CreateService();
        await service.GenerateBriefingAsync(directorId, "kc-dir", "Dir", "PortfolioDirector");

        capturedContext.Should().Contain("Portfolios managed: 1");
        capturedContext.Should().Contain("Total projects: 2");
        capturedContext.Should().Contain("Projects without PM: 1");
        capturedContext.Should().Contain("My own personal pending tasks: 1");
    }

    [Fact]
    public async Task GenerateBriefingAsync_UnknownRole_UsesDefaultContext()
    {
        string? capturedContext = null;
        _summaryService
            .Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((context, _) => capturedContext = context)
            .ReturnsAsync("ok");

        var service = CreateService();
        await service.GenerateBriefingAsync(Guid.NewGuid(), "kc-x", "Mystery", "SuperAdmin");

        capturedContext.Should().Be("User: Mystery, Role: SuperAdmin.");
    }

    [Fact]
    public async Task GenerateBriefingAsync_ReturnsRawContext_WhenSummarizeAsyncThrows()
    {
        _summaryService
            .Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        var service = CreateService();

        var result = await service.GenerateBriefingAsync(Guid.NewGuid(), "kc-x", "Mystery", "SuperAdmin");

        result.Should().Be("User: Mystery, Role: SuperAdmin.");
    }
}
