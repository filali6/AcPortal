using Backend.Kafka;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Backend.Modules.Events.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly KafkaProducerService _producer;
    private readonly IConfiguration _configuration;

    public EventsController(KafkaProducerService producer, IConfiguration configuration)
    {
        _producer = producer;
        _configuration = configuration;
    }

    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromBody] PublishEventRequest request)
    {
        var message = JsonSerializer.Serialize(new
        {
            toolName = request.ToolName,
            eventType = request.EventType
        });

        var topic = _configuration["Kafka:TopicName"];
        await _producer.PublishAsync(topic!, message);

        return Ok(new
        {
            success = true,
            message = "Événement publié sur Kafka",
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