using Backend.Modules.Auth.Models;

namespace Backend.Modules.Projects.Models;

public class Stream
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid? BusinessTeamLeadId { get; set; }
    public User? BusinessTeamLead { get; set; }

    public Guid? TechnicalTeamLeadId { get; set; }
    public User? TechnicalTeamLead { get; set; }

    public ICollection<StreamMember> Members { get; set; } = new List<StreamMember>();
}