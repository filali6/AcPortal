namespace Backend.Modules.Sla.Models;

public class SlaWeeklyReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = string.Empty; // rapport markdown généré par l'agent IA
    public int OverdueTasksCount { get; set; }
    public int AtRiskTasksCount { get; set; }
    public int OverdueStreamsCount { get; set; }
    public int AtRiskStreamsCount { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}