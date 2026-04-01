namespace Backend.Modules.Projects.Models;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();

    
    public string Name { get; set; } = string.Empty;

    
    public string Description { get; set; } = string.Empty;

    public Guid? PortfolioId { get; set; }

    public Guid PortfolioDirectorId { get; set; }

    public Guid? ProjectManagerId { get; set; }


    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}