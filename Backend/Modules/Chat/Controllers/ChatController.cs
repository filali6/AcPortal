using Backend.Data;
using Backend.Modules.Chat.Models;
using Backend.Modules.Chat.Services;
using Backend.Modules.Contracts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Chat.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;
    private readonly IContractSummaryService _summaryService;
    private readonly AppDbContext _db;

    public ChatController(ChatService chatService, IContractSummaryService summaryService, AppDbContext db)
    {
        _chatService = chatService;
        _summaryService = summaryService;
        _db = db;
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

    [HttpPost("summarize")]
    public async Task<IActionResult> Summarize([FromBody] SummarizeRequest request)
    {
        IQueryable<ChatMessage> query = _db.ChatMessages.OrderByDescending(m => m.CreatedAt);

        if (request.StreamId.HasValue)
            query = query.Where(m => m.StreamId == request.StreamId);
        else if (request.TaskId.HasValue)
            query = query.Where(m => m.TaskId == request.TaskId);
        else
            return BadRequest(new { message = "Provide StreamId or TaskId" });

        var messages = await query
            .Take(50)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        if (!messages.Any())
            return Ok(new { summary = "No messages to summarize." });

        var transcript = string.Join("\n", messages.Select(m =>
            $"[{m.CreatedAt:HH:mm}] {m.SenderName}: {m.Content}"));

        var prompt = """
            You are an assistant summarizing a project team discussion.
            Summarize the following conversation in exactly 3 short sentences:
            1. What was discussed or decided
            2. What is blocked or pending
            3. What is the next action

            Be concise and use business language. with  bullet points.
            """;

        var summary = await _summaryService.SummarizeAsync(transcript, prompt);

        return Ok(new { summary });
    }

    public class SummarizeRequest
    {
        public Guid? StreamId { get; set; }
        public Guid? TaskId { get; set; }
    }
}