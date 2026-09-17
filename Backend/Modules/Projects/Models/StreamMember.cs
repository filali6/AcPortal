using Backend.Modules.Auth.Models;
namespace Backend.Modules.Projects.Models;

public enum TeamType
{
    Business=0 ,
    Technical = 1
}
public class StreamMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StreamId { get; set; }
    public Stream Stream { get; set; } = null!;
    public Guid ConsultantId { get; set; }
    public User Consultant { get; set; } = null!;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public TeamType TeamType {get;set;}
}