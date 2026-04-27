namespace Backend.Modules.Projects.Models;

public class Stream
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    // stream appartient à un projet
    public Guid ProjectId { get; set; }

    // les deux leads du stream
    public Guid? BusinessTeamLeadId { get; set; }
    public Guid? TechnicalTeamLeadId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}