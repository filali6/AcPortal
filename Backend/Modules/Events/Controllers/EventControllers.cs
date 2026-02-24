using Backend.Kafka;
using Microsoft.AspNetCore.Mvc;
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

    // POST api/events/publish
    [HttpPost("publish")]
    public async Task<IActionResult> Publish([FromBody] PublishEventRequest request)
    {
        var message = JsonSerializer.Serialize(new
        {
            toolName = request.ToolName,
            eventType = request.EventType,
            payload = request.Payload
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
    public string ToolName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
}