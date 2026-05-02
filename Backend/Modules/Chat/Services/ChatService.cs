using Backend.Data;
using Backend.Modules.Chat.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Chat.Services;

public class ChatService
{
    private readonly AppDbContext _db;

    public ChatService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ChatMessage> SaveMessageAsync(
        string content,
        string senderKeycloakId,
        string senderName,
        Guid? streamId,
        Guid? taskId)
    {
        var message = new ChatMessage
        {
            Content = content,
            SenderKeycloakId = senderKeycloakId,
            SenderName = senderName,
            StreamId = streamId,
            TaskId = taskId
        };

        _db.ChatMessages.Add(message);
        await _db.SaveChangesAsync();
        return message;
    }

    public async Task<List<ChatMessage>> GetByStreamAsync(Guid streamId)
    {
        return await _db.ChatMessages
            .Where(m => m.StreamId == streamId && m.TaskId == null)
            .OrderBy(m => m.CreatedAt)
            .Take(100)
            .ToListAsync();
    }

    public async Task<List<ChatMessage>> GetByTaskAsync(Guid taskId)
    {
        return await _db.ChatMessages
            .Where(m => m.TaskId == taskId)
            .OrderBy(m => m.CreatedAt)
            .Take(100)
            .ToListAsync();
    }
}