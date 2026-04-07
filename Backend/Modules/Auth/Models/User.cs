namespace Backend.Modules.Auth.Models;

public enum GlobalRole
{
    HeadOfCDS = 0,          
    PortfolioDirector = 1,   
    ProjectManager = 2,     
    BusinessTeamLead = 3,    
    TechnicalTeamLead = 4,   
    Consultant = 5   ,
    DAF =6

}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

     public string FullName { get; set; } = string.Empty;

     
    public string Email { get; set; } = string.Empty;


    public string KeycloakId { get; set; } = string.Empty;


    public GlobalRole Role { get; set; } = GlobalRole.Consultant;

    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}