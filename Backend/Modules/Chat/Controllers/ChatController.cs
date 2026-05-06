using Backend.Modules.Chat.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Modules.Chat.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;

    public ChatController(ChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpGet("stream/{streamId}")]
    public async Task<IActionResult> GetStreamMessages(Guid streamId)
    {
        var messages = await _chatService.GetByStreamAsync(streamId);
        return Ok(messages);
    }

    [HttpGet("task/{taskId}")]
    public async Task<IActionResult> GetTaskMessages(Guid taskId)
    {
        var messages = await _chatService.GetByTaskAsync(taskId);
        return Ok(messages);
    }
}