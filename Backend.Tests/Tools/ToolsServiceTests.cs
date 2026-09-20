using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Tools.Models;
using Backend.Modules.Tools.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Tests.Tools;

public class ToolsServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ToolsService _service;

    public ToolsServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _service = new ToolsService(_db, NullLogger<ToolsService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CreateToolAsync_PersistsTool()
    {
        var tool = await _service.CreateToolAsync("Jira", "desc");

        tool.Id.Should().NotBeEmpty();
        (await _db.AcpTools.FindAsync(tool.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllToolsAsync_ReturnsAllTools()
    {
        await _service.CreateToolAsync("Jira", "d1");
        await _service.CreateToolAsync("Confluence", "d2");

        var result = await _service.GetAllToolsAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateRoleAsync_WhenToolMissing_ReturnsNull()
    {
        var role = await _service.CreateRoleAsync("Admin", Guid.NewGuid());

        role.Should().BeNull();
    }

    [Fact]
    public async Task CreateRoleAsync_WhenToolExists_PersistsRole()
    {
        var tool = await _service.CreateToolAsync("Jira", "desc");

        var role = await _service.CreateRoleAsync("Admin", tool.Id);

        role.Should().NotBeNull();
        role!.ToolId.Should().Be(tool.Id);
    }

    [Fact]
    public async Task GetRolesByToolAsync_ReturnsOnlyMatchingRoles()
    {
        var tool = await _service.CreateToolAsync("Jira", "desc");
        var otherTool = await _service.CreateToolAsync("Confluence", "desc");
        await _service.CreateRoleAsync("Admin", tool.Id);
        await _service.CreateRoleAsync("Viewer", otherTool.Id);

        var result = await _service.GetRolesByToolAsync(tool.Id);

        result.Should().ContainSingle(r => r.Name == "Admin");
    }

    [Fact]
    public async Task AssignRoleAsync_WhenConsultantMissing_ReturnsNull()
    {
        var tool = await _service.CreateToolAsync("Jira", "desc");
        var role = await _service.CreateRoleAsync("Admin", tool.Id);

        var result = await _service.AssignRoleAsync(Guid.NewGuid(), tool.Id, role!.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AssignRoleAsync_WhenAlreadyAssigned_ReturnsNull()
    {
        var consultant = new User { FullName = "C", Email = "c@test.com", KeycloakId = "kc" };
        _db.Users.Add(consultant);
        await _db.SaveChangesAsync();
        var tool = await _service.CreateToolAsync("Jira", "desc");
        var role = await _service.CreateRoleAsync("Admin", tool.Id);
        await _service.AssignRoleAsync(consultant.Id, tool.Id, role!.Id);

        var result = await _service.AssignRoleAsync(consultant.Id, tool.Id, role.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AssignRoleAsync_WithValidData_PersistsAssignment()
    {
        var consultant = new User { FullName = "C", Email = "c@test.com", KeycloakId = "kc" };
        _db.Users.Add(consultant);
        await _db.SaveChangesAsync();
        var tool = await _service.CreateToolAsync("Jira", "desc");
        var role = await _service.CreateRoleAsync("Admin", tool.Id);

        var result = await _service.AssignRoleAsync(consultant.Id, tool.Id, role!.Id);

        result.Should().NotBeNull();
        (await _service.HasAccessAsync(consultant.Id, tool.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task GetMyRolesGroupedAsync_GroupsRoleNamesByTool()
    {
        var consultant = new User { FullName = "C", Email = "c@test.com", KeycloakId = "kc" };
        _db.Users.Add(consultant);
        await _db.SaveChangesAsync();
        var tool = await _service.CreateToolAsync("Jira", "desc");
        var role = await _service.CreateRoleAsync("Admin", tool.Id);
        await _service.AssignRoleAsync(consultant.Id, tool.Id, role!.Id);

        var result = await _service.GetMyRolesGroupedAsync(consultant.Id);

        result.Should().ContainSingle();
        var toolNameProp = result[0].GetType().GetProperty("toolName")!.GetValue(result[0]);
        toolNameProp.Should().Be("Jira");
    }

    [Fact]
    public async Task HasAccessAsync_WhenNoRole_ReturnsFalse()
    {
        var result = await _service.HasAccessAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeFalse();
    }
}
