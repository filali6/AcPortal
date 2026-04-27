 
namespace Backend.Modules.Projects.Models;

public class ProjectStep
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public Guid? StreamId { get; set; }
    public Stream? Stream { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public int Order { get; set; }
    public bool CanBeParallel { get; set; } = false;
    public Guid? DependsOnStepId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}