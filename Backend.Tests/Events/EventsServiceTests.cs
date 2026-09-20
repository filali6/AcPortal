using Backend.Data;
using Backend.Modules.Events.Models;
using Backend.Modules.Events.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Backend.Tests.Events;

public class EventsServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly EventsService _service;

    public EventsServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _service = new EventsService(_db, NullLogger<EventsService>.Instance);
    }

   public void Dispose()
{
    _db.Dispose();
    GC.SuppressFinalize(this);
}

    [Fact]
    public async Task GetAllAsync_ReturnsEventsOrderedByReceivedAtDescending()
    {
        _db.AcpEvents.AddRange(
            new AcpEvent { EventType = "Old", ReceivedAt = DateTime.UtcNow.AddMinutes(-10) },
            new AcpEvent { EventType = "New", ReceivedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var result = await _service.GetAllAsync();

        result.Should().HaveCount(2);
        result[0].EventType.Should().Be("New");
        result[1].EventType.Should().Be("Old");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsEvent_WhenFound()
    {
        var acpEvent = new AcpEvent { EventType = "ContratSigné" };
        _db.AcpEvents.Add(acpEvent);
        await _db.SaveChangesAsync();

        var result = await _service.GetByIdAsync(acpEvent.Id);

        result.Should().NotBeNull();
        result!.EventType.Should().Be("ContratSigné");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }
}
