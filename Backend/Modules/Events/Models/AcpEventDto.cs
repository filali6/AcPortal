namespace Backend.Modules.Events.Models;

public class AcpEventDto
{
    public string ToolName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;

    public Guid? DirectorId { get; set; }
    public Guid? ChefEquipeId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? StepId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ProjectManagerId { get; set; }
    public Guid? BusinessTeamLeadId { get; set; }
    public Guid? TechnicalTeamLeadId { get; set; }
    public Guid? StreamId { get; set; }

    // Contrat DAF
    public string? ClientName { get; set; }
    public Guid? ContractId { get; set; }
    public string? ProjectName { get; set; }
}