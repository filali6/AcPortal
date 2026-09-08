using Backend.Data;
using Backend.Modules.Tasks.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Tasks.Controllers;

[ApiController]
[Route("api/tasks/{taskId}/comments")]
public class TaskCommentsController : ControllerBase
{
    private readonly TaskCommentsService _commentsService;
    private readonly AppDbContext _db;

    public TaskCommentsController(TaskCommentsService commentsService, AppDbContext db)
    {
        _commentsService = commentsService;
        _db = db;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetComments(Guid taskId)
    {
        var comments = await _commentsService.GetCommentsAsync(taskId);
        return Ok(comments);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> AddComment(Guid taskId, [FromBody] CreateCommentDto dto)
    {
        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.KeycloakId == keycloakId);
        if (user == null) return NotFound();

        var comment = await _commentsService.AddCommentAsync(
            taskId,
            dto.Content,
            keycloakId,
            user.FullName,
            dto.ParentCommentId,
            dto.Mentions
        );

        return Ok(new
        {
            comment.Id,
            comment.Content,
            comment.AuthorName,
            comment.AuthorKeycloakId,
            comment.CreatedAt,
            comment.Mentions,
            comment.ParentCommentId
        });
    }

    [HttpDelete("{commentId}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(Guid taskId, Guid commentId)
    {
        var keycloakId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (keycloakId == null) return Unauthorized();

        var deleted = await _commentsService.DeleteCommentAsync(commentId, taskId, keycloakId);
        if (!deleted) return Forbid();

        return Ok();
    }
}

public class CreateCommentDto
{
    public string Content { get; set; } = string.Empty;
    public Guid? ParentCommentId { get; set; }
    public List<string>? Mentions { get; set; }
}