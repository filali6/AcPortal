using Backend.Data;
using Backend.Modules.Events.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Backend.Modules.Events.Services;
namespace Backend.Modules.Events.Controllers;


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
        // Au lieu de publier sur Kafka directement
        // on écrit dans OutboxMessages en base
        var outboxMessage = new OutboxMessage
        {
            EventType = request.EventType,
            ToolName = request.ToolName,
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
                toolName = request.ToolName,
                eventType = request.EventType
            }
        });
    }
    // GET api/events → liste tous les événements
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var events = await _eventsService.GetAllAsync();
        return Ok(events);
    }

    // GET api/events/{id} → détail d'un événement
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var acpEvent = await _eventsService.GetByIdAsync(id);
        if (acpEvent == null)
            return NotFound(new { message = "Événement introuvable" });

        return Ok(acpEvent);
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
}
 