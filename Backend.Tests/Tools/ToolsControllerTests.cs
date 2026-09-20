using System.Security.Claims;
using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Tools.Controllers;
using Backend.Modules.Tools.Models;
using Backend.Modules.Tools.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Tests.Tools;

public class ToolsControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ToolsService _service;
    private readonly ToolsController _controller;

    public ToolsControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _service = new ToolsService(_db, NullLogger<ToolsService>.Instance);
        _controller = new ToolsController(_service, _db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void SetUser(ToolsController controller, string keycloakId)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, keycloakId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [Fact]
    public async Task GetAll_ReturnsAllTools()
    {
        _db.AcpTools.Add(new AcpTool { Name = "Tool1" });
        await _db.SaveChangesAsync();

        var result = await _controller.GetAll();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<AcpTool>)ok.Value!).Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateTool_PersistsAndReturnsOk()
    {
        var result = await _controller.CreateTool(new CreateToolRequest { Name = "Jira", Description = "desc" });

        result.Should().BeOfType<OkObjectResult>();
        (await _db.AcpTools.SingleAsync()).Name.Should().Be("Jira");
    }

    [Fact]
    public async Task CreateRole_WhenToolNotFound_ReturnsNotFound()
    {
        var result = await _controller.CreateRole(Guid.NewGuid(), new CreateRoleRequest { Name = "Admin" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateRole_WhenToolExists_ReturnsOk()
    {
        var tool = new AcpTool { Name = "Tool1" };
        _db.AcpTools.Add(tool);
        await _db.SaveChangesAsync();

        var result = await _controller.CreateRole(tool.Id, new CreateRoleRequest { Name = "Admin" });

        result.Should().BeOfType<OkObjectResult>();
        (await _db.ToolRoles.SingleAsync()).ToolId.Should().Be(tool.Id);
    }

    [Fact]
    public async Task AssignRole_WithInvalidData_ReturnsBadRequest()
    {
        var result = await _controller.AssignRole(new AssignRoleRequest
        {
            ConsultantId = Guid.NewGuid(),
            ToolId = Guid.NewGuid(),
            ToolRoleId = Guid.NewGuid()
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AssignRole_WithValidData_ReturnsOk()
    {
        var consultant = new User { FullName = "C", Email = "c@test.com", KeycloakId = "kc" };
        var tool = new AcpTool { Name = "Tool1" };
        var role = new ToolRole { Name = "Admin", ToolId = tool.Id };
        _db.Users.Add(consultant);
        _db.AcpTools.Add(tool);
        _db.ToolRoles.Add(role);
        await _db.SaveChangesAsync();

        var result = await _controller.AssignRole(new AssignRoleRequest
        {
            ConsultantId = consultant.Id,
            ToolId = tool.Id,
            ToolRoleId = role.Id
        });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CheckAccess_WhenNoRole_ReturnsFalse()
    {
        var result = await _controller.CheckAccess(Guid.NewGuid(), Guid.NewGuid());

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value!.GetType().GetProperty("hasAccess")!.GetValue(ok.Value).Should().Be(false);
    }

    [Fact]
    public async Task GetMyRoles_WhenUnauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await _controller.GetMyRoles();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetMyRoles_WhenUserNotFound_ReturnsNotFound()
    {
        SetUser(_controller, "unknown-kc-id");

        var result = await _controller.GetMyRoles();

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMyRoles_WhenUserFound_ReturnsGroupedRoles()
    {
        var user = new User { FullName = "U", Email = "u@test.com", KeycloakId = "kc-u" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        SetUser(_controller, "kc-u");

        var result = await _controller.GetMyRoles();

        result.Should().BeOfType<OkObjectResult>();
    }
}
