using Backend.Modules.Auth.Models;
namespace Backend.Modules.Projects.Models;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? PortfolioId { get; set; }
    public Portfolio? Portfolio { get; set; }

    public Guid? ProjectManagerId { get; set; }
    public User? ProjectManager { get; set; }

    public ICollection<Stream> Streams { get; set; } = new List<Stream>();
}