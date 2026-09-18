using Backend.Data;
using Backend.Modules.Tools.Adapters;
using Backend.Modules.Tools.Models;
using Backend.Modules.Tools.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests.Tools;

public class PluginRegistryTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly PluginRegistry _registry;

    public PluginRegistryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _registry = new PluginRegistry(_db);
    }

    public void Dispose() => _db.Dispose();

    private static PluginDefinition MakeDefinition(string id, string url = "https://plugin.test") => new()
    {
        Id = id,
        Name = $"Plugin {id}",
        Description = "desc",
        Category = "cat",
        Icon = "icon",
        Url = url
    };

    [Fact]
    public void GetAll_ReturnsAllPersistedDefinitions()
    {
        _db.PluginDefinitions.AddRange(MakeDefinition("p1"), MakeDefinition("p2"));
        _db.SaveChanges();

        var result = _registry.GetAll();

        result.Should().HaveCount(2);
    }

    [Fact]
    public void GetById_ReturnsMatchingDefinition()
    {
        _db.PluginDefinitions.Add(MakeDefinition("p1"));
        _db.SaveChanges();

        var result = _registry.GetById("p1");

        result.Should().NotBeNull();
        result!.Name.Should().Be("Plugin p1");
    }

    [Fact]
    public void GetById_ReturnsNull_WhenNotFound()
    {
        var result = _registry.GetById("missing");

        result.Should().BeNull();
    }

    [Fact]
    public void GetAdapter_ReturnsGenericAdapter_WithUrlFromDefinition()
    {
        _db.PluginDefinitions.Add(MakeDefinition("p1", "https://tool.example.com"));
        _db.SaveChanges();

        var adapter = _registry.GetAdapter("p1");

        adapter.Should().BeOfType<GenericAdapter>();
        adapter!.PluginId.Should().Be("p1");
        adapter.GetAccessUrl().Should().Be("https://tool.example.com");
    }

    [Fact]
    public void GetAdapter_ReturnsNull_WhenDefinitionMissing()
    {
        var adapter = _registry.GetAdapter("missing");

        adapter.Should().BeNull();
    }

    [Fact]
    public void AddDefinition_InsertsNewDefinition()
    {
        _registry.AddDefinition(MakeDefinition("p1"));

        _db.PluginDefinitions.Should().ContainSingle(d => d.Id == "p1");
    }

    [Fact]
    public void AddDefinition_ReplacesExistingDefinition_WithSameId()
    {
        _registry.AddDefinition(MakeDefinition("p1", "https://old.test"));

        _registry.AddDefinition(MakeDefinition("p1", "https://new.test"));

        var definitions = _db.PluginDefinitions.Where(d => d.Id == "p1").ToList();
        definitions.Should().ContainSingle();
        definitions[0].Url.Should().Be("https://new.test");
    }

    [Fact]
    public void RemoveDefinition_DeletesExistingDefinition()
    {
        _registry.AddDefinition(MakeDefinition("p1"));

        _registry.RemoveDefinition("p1");

        _db.PluginDefinitions.Any(d => d.Id == "p1").Should().BeFalse();
    }

    [Fact]
    public void RemoveDefinition_NoOp_WhenNotFound()
    {
        var act = () => _registry.RemoveDefinition("missing");

        act.Should().NotThrow();
    }
}
