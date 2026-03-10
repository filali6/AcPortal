namespace Backend.Modules.Projects.Models;

public class TeamMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TeamId { get; set; }
    public Guid ConsultantId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}