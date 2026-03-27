using System.ComponentModel.DataAnnotations;
namespace Backend.Modules.Projects.Models;


public class StepDto
{
    [Required]
    public string StepName { get; set; } = string.Empty;

    [Required]
    public string ToolName { get; set; } = string.Empty;

    public int Order { get; set; }
    public bool CanBeParallel { get; set; } = false;
    //public Guid? DependsOnStepId { get; set; }
    public string? DependsOnStepId { get; set; }
}