using Backend.Data;
using Backend.Modules.Events.Controllers;
using Backend.Modules.Events.Models;
using Backend.Modules.Events.Services;
using Dapr.Client;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Tests.Events;

public class EventsControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly EventsController _controller;

    public EventsControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        var eventsService = new EventsService(_db, NullLogger<EventsService>.Instance);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventPublisher:MaxRetries"] = "0"
        }).Build();
        var eventPublisher = new EventPublisher(new DaprClientBuilder().Build(), NullLogger<EventPublisher>.Instance, config);
        _controller = new EventsController(_db, eventsService, eventPublisher);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Publish_ReturnsOkWithEventData()
    {
        var result = await _controller.Publish(new PublishEventRequest { EventType = "Test", ToolName = "Tool" });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetAll_ReturnsAllEvents()
    {
        _db.AcpEvents.Add(new AcpEvent { EventType = "Test", ToolName = "Tool" });
        await _db.SaveChangesAsync();

        var result = await _controller.GetAll();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<AcpEvent>)ok.Value!).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        var result = await _controller.GetById(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetById_WhenFound_ReturnsOk()
    {
        var acpEvent = new AcpEvent { EventType = "Test", ToolName = "Tool" };
        _db.AcpEvents.Add(acpEvent);
        await _db.SaveChangesAsync();

        var result = await _controller.GetById(acpEvent.Id);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task TriggerContract_ReturnsOk()
    {
        var result = await _controller.TriggerContract();

        result.Should().BeOfType<OkObjectResult>();
    }
}
