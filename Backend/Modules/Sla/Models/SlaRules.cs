namespace Backend.Modules.Sla.Models;

public enum SlaRuleType
{
    Stream,
    Task
}

public enum SlaStatus
{
    OnTrack,
    AtRisk,
    Overdue
}

public class SlaRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SlaRuleType Type { get; set; } = SlaRuleType.Task;
    public int SlaDays { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}