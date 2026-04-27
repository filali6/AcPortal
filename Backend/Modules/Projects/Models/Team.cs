namespace Backend.Modules.Projects.Models;

public class Team
{
    public Guid Id { get; set; } = Guid.NewGuid();

    
    public string Name { get; set; } = string.Empty;

     
    public Guid ProjectId { get; set; }

     
    public Guid ChefEquipeId { get; set; }

     
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}