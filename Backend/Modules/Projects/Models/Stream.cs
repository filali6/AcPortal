using Backend.Modules.Auth.Models;
using Backend.Modules.Sla.Models;

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

    public string? GitRepoUrl{get;set;}

  

    public string? MessagingChannelId { get; set; }
    public string? MessagingChannelUrl { get; set; }
    // Ajouter dans Stream.cs
    public DateTime? DueDate { get; set; }
    public SlaStatus SlaStatus { get; set; } = SlaStatus.OnTrack;
}