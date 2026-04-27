namespace Backend.Modules.Events.Models;

// représente UNE règle dans le JSON
public class WorkflowRule
{
    // "ContratSigné", "ProjetCréé"...
    public string EventCode { get; set; } = string.Empty;

    // "CREATE_TASK" | "CREATE_TASKS_FROM_STEPS" | "UNBLOCK_DEPENDENT_STEPS"
    public string ActionType { get; set; } = string.Empty;

    // titre de la tâche à créer — null si pas CREATE_TASK
    public string? TaskTitle { get; set; }

    // description — null si pas CREATE_TASK
    public string? TaskDescription { get; set; }

    // "ROLE" | "CONTEXT_USER" | "BEST_CONSULTANT" | "CONTEXT"
    public string TargetType { get; set; } = string.Empty;

    // "HeadOfCDS" | "DirectorId" | "ChefEquipeId" | "ProjectId" | "StepId"
    public List<string>TargetValues { get; set; } = new();
}

// représente la racine du JSON { "workflowRules": [...] }
public class WorkflowConfig
{
    public List<WorkflowRule> WorkflowRules { get; set; } = new();
}