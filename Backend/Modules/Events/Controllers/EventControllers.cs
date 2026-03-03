using Backend.Data;
using Backend.Modules.Events.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Backend.Modules.Events.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly AppDbContext _db;

    public EventsController(AppDbContext db)
    {
        _db = db;
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
 