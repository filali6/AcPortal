namespace Backend.Modules.Chat.Models;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = string.Empty;
    public string SenderKeycloakId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public Guid? StreamId { get; set; }
    public Guid? TaskId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}