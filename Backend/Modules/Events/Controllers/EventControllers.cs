using Backend.Data;
using Backend.Modules.Events.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Backend.Modules.Events.Services;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

namespace Backend.Modules.Events.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EventsService _eventsService;
    private readonly EventPublisher _eventPublisher;

    public EventsController(
        AppDbContext db,
        EventsService eventsService,
        EventPublisher eventPublisher)
    {
        _db = db;
        _eventsService = eventsService;
        _eventPublisher = eventPublisher;
    }

    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromBody] PublishEventRequest request)
    {
        await _eventPublisher.PublishAsync(new
        {
            eventType = request.EventType,
            toolName = request.ToolName,
            projectId = request.ProjectId
        }, request.ProjectId);

        return Ok(new
        {
            success = true,
            message = "Événement publié via Dapr",
            data = new
            {
                eventType = request.EventType,
                toolName = request.ToolName,
                projectId = request.ProjectId
            }
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var events = await _eventsService.GetAllAsync();
        return Ok(events);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var acpEvent = await _eventsService.GetByIdAsync(id);
        if (acpEvent == null)
            return NotFound(new { message = "Événement introuvable" });
        return Ok(acpEvent);
    }

    [HttpPost("trigger-contract")]
    [Authorize(Roles = "DAF")]
    public async Task<IActionResult> TriggerContract()
    {
        // ✅ EventPublisher au lieu de DaprClient directement
        await _eventPublisher.PublishAsync(new
        {
            eventType = "ContratSigné"
        });

        return Ok(new
        {
            message = "Contrat signé — projet en cours de création"
        });
    }
}

public class PublishEventRequest
{
    [Required]
    [MaxLength(100)]
    public string ToolName { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string EventType { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }
}