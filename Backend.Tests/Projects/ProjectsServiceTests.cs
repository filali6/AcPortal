using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Projects.Models;
using Backend.Modules.Projects.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Tests.Projects;

public class ProjectsServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ProjectsService _service;

    public ProjectsServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _service = new ProjectsService(_db, NullLogger<ProjectsService>.Instance);
    }

   public void Dispose()
{
    _db.Dispose();
    GC.SuppressFinalize(this);
}

    [Fact]
    public async Task CreateAsync_WithoutTargetDate_PersistsProject()
    {
        var portfolioId = Guid.NewGuid();

        var result = await _service.CreateAsync("Project X", "desc", portfolioId);

        result.Id.Should().NotBeEmpty();
        result.TargetDate.Should().BeNull();
        (await _db.Projects.FindAsync(result.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_WithTargetDate_StoresUtcTargetDate()
    {
        var unspecified = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Unspecified);

        var result = await _service.CreateAsync("Project Y", "desc", Guid.NewGuid(), unspecified);

        result.TargetDate!.Value.Kind.Should().Be(DateTimeKind.Utc);
        result.TargetDate!.Value.Should().Be(DateTime.SpecifyKind(unspecified, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetAllAsync_ReturnsProjectsOrderedByCreatedAtDescending_WithManagerName()
    {
        var pm = new User { FullName = "Manager", Email = "m@test.com", KeycloakId = "kc-m" };
        _db.Users.Add(pm);
        var older = new Project { Name = "Older", CreatedAt = DateTime.UtcNow.AddDays(-1) };
        var newer = new Project { Name = "Newer", CreatedAt = DateTime.UtcNow, ProjectManagerId = pm.Id, ProjectManager = pm };
        _db.Projects.AddRange(older, newer);
        await _db.SaveChangesAsync();

        var result = await _service.GetAllAsync();

        result.Should().HaveCount(2);
        var first = result[0].GetType().GetProperty("Name")!.GetValue(result[0]);
        first.Should().Be("Newer");
        var managerName = result[0].GetType().GetProperty("projectManagerName")!.GetValue(result[0]);
        managerName.Should().Be("Manager");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsProject_WhenExists()
    {
        var project = new Project { Name = "P" };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        var result = await _service.GetByIdAsync(project.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("P");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMyProjectsAsync_ReturnsOnlyProjectsUnderDirectorsPortfolios()
    {
        var directorId = Guid.NewGuid();
        var myPortfolio = new Portfolio { Name = "Mine", PortfolioDirectorId = directorId };
        var otherPortfolio = new Portfolio { Name = "Other", PortfolioDirectorId = Guid.NewGuid() };
        _db.Portfolios.AddRange(myPortfolio, otherPortfolio);
        await _db.SaveChangesAsync();
        _db.Projects.Add(new Project { Name = "MyProject", PortfolioId = myPortfolio.Id });
        _db.Projects.Add(new Project { Name = "OtherProject", PortfolioId = otherPortfolio.Id });
        await _db.SaveChangesAsync();

        var result = await _service.GetMyProjectsAsync(directorId);

        result.Should().ContainSingle();
        result[0].GetType().GetProperty("Name")!.GetValue(result[0]).Should().Be("MyProject");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_WhenProjectNotFound()
    {
        var result = await _service.UpdateAsync(Guid.NewGuid(), "New", "desc", null);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFields_WhenProjectExists()
    {
        var project = new Project { Name = "Old", Description = "Old desc" };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        var newTargetDate = new DateTime(2027, 5, 1, 0, 0, 0, DateTimeKind.Unspecified);

        var result = await _service.UpdateAsync(project.Id, "New", "New desc", newTargetDate);

        result.Should().NotBeNull();
        result!.Name.Should().Be("New");
        result.Description.Should().Be("New desc");
        result.TargetDate!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task UpdateAsync_ClearsTargetDate_WhenNullPassed()
    {
        var project = new Project { Name = "Old", TargetDate = DateTime.UtcNow };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        var result = await _service.UpdateAsync(project.Id, "Old", "desc", null);

        result!.TargetDate.Should().BeNull();
    }
}
