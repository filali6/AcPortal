namespace Backend.Modules.Auth.Models;

public enum GlobalRole
{
    SuperAdmin,
    PortfolioDirector,
    ChefEquipe,
    Consultant,
    DAF
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

     public string FullName { get; set; } = string.Empty;

     
    public string Email { get; set; } = string.Empty;

     
    public string PasswordHash { get; set; } = string.Empty;

    
    public GlobalRole Role { get; set; } = GlobalRole.Consultant;

    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}