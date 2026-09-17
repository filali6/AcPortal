namespace Backend.Modules.Planning.Models;

public class PlanningProposal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Status { get; set; } = "Draft";
    public string ProposalJson { get; set; } = string.Empty;
    public string ConversationJson { get; set; } = "[]";
    public string? FsdFileName { get; set; }
    public string? Guidelines { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}