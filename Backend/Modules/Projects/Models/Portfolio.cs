using Backend.Modules.Auth.Models;
namespace Backend.Modules.Projects.Models;

public class Portfolio
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? PortfolioDirectorId { get; set; }
    public User? PortfolioDirector { get; set; }

    public ICollection<Project> Projects { get; set; } = new List<Project>();
}