namespace Backend.Modules.Projects.Models;

public class StepConfigFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StepId { get; set; }
    public ProjectStep Step { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string CommitHash { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string UploadedBy { get; set; } = string.Empty;
}