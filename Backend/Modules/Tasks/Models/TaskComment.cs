namespace Backend.Modules.Tasks.Models;

public class TaskComment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = string.Empty;
    public string AuthorKeycloakId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public Guid TaskId { get; set; }
    public AcpTask Task { get; set; } = null!;
    public Guid? ParentCommentId { get; set; }
    public TaskComment? ParentComment { get; set; }
    public ICollection<TaskComment> Replies { get; set; } = new List<TaskComment>();
    public List<string> Mentions { get; set; } = new List<string>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}