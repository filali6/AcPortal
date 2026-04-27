namespace Backend.Modules.Projects.Models;

public class StreamMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StreamId { get; set; }
    public Guid ConsultantId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}