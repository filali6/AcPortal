using Backend.Data;
using Backend.Modules.Sla.Controllers;
using Backend.Modules.Sla.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests.Sla;

public class SlaRulesControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SlaRulesController _controller;

    public SlaRulesControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _controller = new SlaRulesController(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithRules()
    {
        _db.SlaRules.Add(new SlaRule { Name = "R1", SlaDays = 5 });
        await _db.SaveChangesAsync();

        var result = await _controller.GetAll();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().ContainSingle();
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenNameMissing()
    {
        var result = await _controller.Create(new CreateSlaRuleRequest { Name = "", SlaDays = 5 });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenSlaDaysNotPositive()
    {
        var result = await _controller.Create(new CreateSlaRuleRequest { Name = "R1", SlaDays = 0 });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_ReturnsOk_PersistsRule()
    {
        var result = await _controller.Create(new CreateSlaRuleRequest { Name = "R1", SlaDays = 5, Type = SlaRuleType.Stream });

        result.Should().BeOfType<OkObjectResult>();
        (await _db.SlaRules.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.Update(Guid.NewGuid(), new UpdateSlaRuleRequest { Name = "New" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Update_ReturnsOk_UpdatesProvidedFields()
    {
        var rule = new SlaRule { Name = "Old", SlaDays = 3, Type = SlaRuleType.Task };
        _db.SlaRules.Add(rule);
        await _db.SaveChangesAsync();

        var result = await _controller.Update(rule.Id, new UpdateSlaRuleRequest { Name = "New", SlaDays = 7, Type = SlaRuleType.Stream });

        result.Should().BeOfType<OkObjectResult>();
        var updated = await _db.SlaRules.FindAsync(rule.Id);
        updated!.Name.Should().Be("New");
        updated.SlaDays.Should().Be(7);
        updated.Type.Should().Be(SlaRuleType.Stream);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.Delete(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Delete_ReturnsOk_RemovesRule()
    {
        var rule = new SlaRule { Name = "R1", SlaDays = 5 };
        _db.SlaRules.Add(rule);
        await _db.SaveChangesAsync();

        var result = await _controller.Delete(rule.Id);

        result.Should().BeOfType<OkObjectResult>();
        (await _db.SlaRules.FindAsync(rule.Id)).Should().BeNull();
    }
}
