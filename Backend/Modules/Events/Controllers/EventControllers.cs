using Backend.Data;
using Backend.Modules.Events.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Backend.Modules.Events.Services;
using Microsoft.AspNetCore.Authorization;
namespace Backend.Modules.Events.Controllers;


using System.Text.Json;


[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly EventsService _eventsService;

    public EventsController(AppDbContext db, EventsService eventsService)
    {
        _db = db;
        _eventsService=eventsService;
    }

    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromBody] PublishEventRequest request)
    {
         
        var topic = request.ProjectId.HasValue
            ? $"project.{request.ProjectId}"
            : "system.events";

        var payload = JsonSerializer.Serialize(new
        {
            eventType = request.EventType,
            toolName = request.ToolName,
            projectId = request.ProjectId
        });

        var outboxMessage = new OutboxMessage
        {
            Topic = topic,
            Payload = payload,
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            Retries = 0
        };

        _db.OutboxMessages.Add(outboxMessage);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Événement enregistré et sera publié sur Kafka",
            data = new
            {
                topic,
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
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            eventType = "ContratSigné"
        });

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Topic = "system.events",
            Payload = payload,
            CreatedAt = DateTime.UtcNow,
            IsProcessed = false,
            Retries = 0
        });

        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Contrat signé — projet en cours de création"
        });
    }
}

public class PublishEventRequest
{
    [Required(ErrorMessage = "ToolName est obligatoire")]
    [MaxLength(100, ErrorMessage = "ToolName ne peut pas dépasser 100 caractères")]
    public string ToolName { get; set; } = string.Empty;

    [Required(ErrorMessage = "EventType est obligatoire")]
    [MaxLength(200, ErrorMessage = "EventType ne peut pas dépasser 200 caractères")]
    public string EventType { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
}
 