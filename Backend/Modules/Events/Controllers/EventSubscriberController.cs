using Backend.Modules.Events.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Backend.Modules.Events.Controllers;

[ApiController]
[Route("api/events")]
public class EventsSubscriberController : ControllerBase
{
    private readonly EventProcessorService _processor;
    private readonly ILogger<EventsSubscriberController> _logger;

    public EventsSubscriberController(
        EventProcessorService processor,
        ILogger<EventsSubscriberController> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    [HttpPost("handle")]
    public async Task<IActionResult> HandleEvent([FromBody] JsonElement payload)
    {
        _logger.LogInformation("Event reçu : {Payload}", payload.ToString());

        Guid? projectId = null;
        Guid? streamId = null;

        if (payload.TryGetProperty("projectId", out var pid) &&
            Guid.TryParse(pid.GetString(), out var parsedProjectId))
            projectId = parsedProjectId;

        if (payload.TryGetProperty("streamId", out var sid) &&
            Guid.TryParse(sid.GetString(), out var parsedStreamId))
            streamId = parsedStreamId;

        await _processor.ProcessAsync(payload.ToString(), projectId, streamId);
        return Ok();
    }
}