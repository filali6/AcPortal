namespace Backend.Modules.Git.Models;
public class GitFileDto
{
    public string FileName { get; set; }
    public string StepName { get; set; }
    public string ToolName { get; set; }
    public string CommitHash { get; set; }
    public string DownloadUrl { get; set; }
    public DateTime LastModified { get; set; }
}