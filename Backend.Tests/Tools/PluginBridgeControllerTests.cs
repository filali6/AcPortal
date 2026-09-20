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
using Xunit;

namespace Backend.Tests.Tools;

public class PluginBridgeControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PluginRegistry _registry;
    private readonly PluginBridgeController _controller;

    public PluginBridgeControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _registry = new PluginRegistry(_db);
        _controller = new PluginBridgeController(_registry, _db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void SetUser(PluginBridgeController controller, string keycloakId)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, keycloakId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private PluginDefinition AddPlugin(bool isActive = true, string allowedRoles = "[\"Consultant\"]")
    {
        var plugin = new PluginDefinition
        {
            Id = "plugin-1",
            Name = "Plugin 1",
            IsActive = isActive,
            AllowedRoles = allowedRoles,
            Url = "http://plugin.test"
        };
        _db.PluginDefinitions.Add(plugin);
        _db.SaveChanges();
        return plugin;
    }

    [Fact]
    public void GetAll_OnlyReturnsActivePluginsWithAllowedRoles()
    {
        AddPlugin();
        AddPlugin(isActive: false) /* inactive, filtered out */;

        var result = _controller.GetAll();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().HaveCount(1);
    }

    [Fact]
    public void GetAllForAdmin_ReturnsEveryPlugin()
    {
        AddPlugin();
        var plugin2 = new PluginDefinition { Id = "plugin-2", Name = "Plugin 2", IsActive = false };
        _db.PluginDefinitions.Add(plugin2);
        _db.SaveChanges();

        var result = _controller.GetAllForAdmin();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().HaveCount(2);
    }

    [Fact]
    public void GetById_WhenNotFound_ReturnsNotFound()
    {
        var result = _controller.GetById("missing");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetById_WhenFound_ReturnsOk()
    {
        AddPlugin();

        var result = _controller.GetById("plugin-1");

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMyPlugins_WhenUnauthenticated_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await _controller.GetMyPlugins();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task AddPlugin_WhenPluginMissing_ReturnsNotFound()
    {
        var user = new User { FullName = "U", Email = "u@test.com", KeycloakId = "kc-1" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        SetUser(_controller, "kc-1");

        var result = await _controller.AddPlugin("missing");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AddPlugin_WhenAlreadyAdded_ReturnsBadRequest()
    {
        var user = new User { FullName = "U", Email = "u@test.com", KeycloakId = "kc-1" };
        AddPlugin();
        _db.Users.Add(user);
        _db.UserPlugins.Add(new UserPlugin { UserId = user.Id, PluginId = "plugin-1" });
        await _db.SaveChangesAsync();
        SetUser(_controller, "kc-1");

        var result = await _controller.AddPlugin("plugin-1");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AddPlugin_WithValidData_PersistsUserPlugin()
    {
        var user = new User { FullName = "U", Email = "u@test.com", KeycloakId = "kc-1" };
        AddPlugin();
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        SetUser(_controller, "kc-1");

        var result = await _controller.AddPlugin("plugin-1");

        result.Should().BeOfType<OkObjectResult>();
        (await _db.UserPlugins.SingleAsync()).PluginId.Should().Be("plugin-1");
    }

    [Fact]
    public async Task RemovePlugin_WhenNotAdded_ReturnsNotFound()
    {
        var user = new User { FullName = "U", Email = "u@test.com", KeycloakId = "kc-1" };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        SetUser(_controller, "kc-1");

        var result = await _controller.RemovePlugin("plugin-1");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task RemovePlugin_WhenAdded_RemovesUserPlugin()
    {
        var user = new User { FullName = "U", Email = "u@test.com", KeycloakId = "kc-1" };
        _db.Users.Add(user);
        _db.UserPlugins.Add(new UserPlugin { UserId = user.Id, PluginId = "plugin-1" });
        await _db.SaveChangesAsync();
        SetUser(_controller, "kc-1");

        var result = await _controller.RemovePlugin("plugin-1");

        result.Should().BeOfType<OkObjectResult>();
        (await _db.UserPlugins.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task CreateTool_WhenAlreadyExists_ReturnsBadRequest()
    {
        AddPlugin();

        var result = await _controller.CreateTool(new PluginDefinition { Id = "plugin-1", Name = "Dup" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateTool_WithNewId_AddsDefinition()
    {
        var result = await _controller.CreateTool(new PluginDefinition { Id = "new-plugin", Name = "New" });

        result.Should().BeOfType<OkObjectResult>();
        _registry.GetById("new-plugin").Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateTool_WhenNotFound_ReturnsNotFound()
    {
        var result = await _controller.UpdateTool("missing", new PluginDefinition { Name = "X" });

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateTool_WhenFound_UpdatesDefinition()
    {
        AddPlugin();

        var result = await _controller.UpdateTool("plugin-1", new PluginDefinition { Name = "Renamed" });

        result.Should().BeOfType<OkObjectResult>();
        _registry.GetById("plugin-1")!.Name.Should().Be("Renamed");
    }

    [Fact]
    public async Task DeleteTool_WhenNotFound_ReturnsNotFound()
    {
        var result = await _controller.DeleteTool("missing");

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteTool_WhenFound_RemovesDefinition()
    {
        AddPlugin();

        var result = await _controller.DeleteTool("plugin-1");

        result.Should().BeOfType<OkObjectResult>();
        _registry.GetById("plugin-1").Should().BeNull();
    }
}
