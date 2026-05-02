namespace Backend.Modules.Notifications.Models;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RecipientKeycloakId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Link { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}