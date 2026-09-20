using System.Text.Json.Serialization;

namespace Backend.Modules.Events.Models;

public class AcpEventDto
{
    public string ToolName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;

    [JsonConverter(typeof(NullableGuidConverter))]
    public Guid? DirectorId { get; set; }
    [JsonConverter(typeof(NullableGuidConverter))]
    public Guid? ChefEquipeId { get; set; }
    [JsonConverter(typeof(NullableGuidConverter))]
    public Guid? TaskId { get; set; }
    [JsonConverter(typeof(NullableGuidConverter))]
    public Guid? StepId { get; set; }
    [JsonConverter(typeof(NullableGuidConverter))]
    public Guid? ProjectId { get; set; }
    [JsonConverter(typeof(NullableGuidConverter))]
    public Guid? ProjectManagerId { get; set; }
    [JsonConverter(typeof(NullableGuidConverter))]
    public Guid? BusinessTeamLeadId { get; set; }
    [JsonConverter(typeof(NullableGuidConverter))]
    public Guid? TechnicalTeamLeadId { get; set; }
    [JsonConverter(typeof(NullableGuidConverter))]
    public Guid? StreamId { get; set; }
    public string? StreamName { get; set; }

    // Contrat DAF
    public string? ClientName { get; set; }
    [JsonConverter(typeof(NullableGuidConverter))]
    public Guid? ContractId { get; set; }
    public string? ProjectName { get; set; }

    public string? LeadRole { get; set; }

    public string? AuthorKeycloakId { get; set; }
    public string? AuthorName { get; set; }
    public string? Content { get; set; }
    public string? TaskTitle { get; set; }

 
   
    public string? AssignedTo { get; set; }
    


}