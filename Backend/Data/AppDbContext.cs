using Backend.Modules.Events.Models;
using Backend.Modules.Tasks.Models;
using Backend.Modules.Auth.Models;
using Backend.Modules.Tools.Models;
using Backend.Modules.Projects.Models;
using Microsoft.EntityFrameworkCore;
 

namespace Backend.Data;

public class AppDbContext : DbContext
{
    // Le constructeur reçoit la configuration (connexion PostgreSQL)
    // depuis Program.cs — on ne la code pas en dur ici
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Ces deux lignes disent à EF Core :
    // "Il existe une table AcpEvents et une table AcpTasks"
    public DbSet<AcpEvent> AcpEvents => Set<AcpEvent>();
    public DbSet<AcpTask> AcpTasks => Set<AcpTask>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<User> Users => Set<User>();

    // Projects
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    // Tools
    public DbSet<AcpTool> AcpTools => Set<AcpTool>();
    public DbSet<ToolRole> ToolRoles => Set<ToolRole>();
    public DbSet<ConsultantToolRole> ConsultantToolRoles => Set<ConsultantToolRole>();



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // On explique à EF Core la relation entre les deux tables :
        // Un AcpEvent a une seule AcpTask (via SourceEventId)
        modelBuilder.Entity<AcpTask>()
            .HasOne<AcpEvent>()
            .WithOne()
            .HasForeignKey<AcpTask>(t => t.SourceEventId);
            
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<Team>().HasIndex(t=>t.ProjectId).IsUnique();
        modelBuilder.Entity<TeamMember>()
            .HasIndex(tm => new { tm.TeamId, tm.ConsultantId })
            .IsUnique();
    }
}