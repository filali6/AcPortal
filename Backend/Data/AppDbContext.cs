using Backend.Modules.Events.Models;
using Backend.Modules.Tasks.Models;
using Backend.Modules.Auth.Models;
using Backend.Modules.Tools.Models;
using Backend.Modules.Projects.Models;
using Microsoft.EntityFrameworkCore;
 

namespace Backend.Data;

public class AppDbContext : DbContext
{
    
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

 
    public DbSet<AcpEvent> AcpEvents => Set<AcpEvent>();
    public DbSet<AcpTask> AcpTasks => Set<AcpTask>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<User> Users => Set<User>();

     
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Backend.Modules.Projects.Models.Stream> Streams => Set<Backend.Modules.Projects.Models.Stream>();
    public DbSet<StreamMember> StreamMembers => Set<StreamMember>();


    public DbSet<AcpTool> AcpTools => Set<AcpTool>();
    public DbSet<ToolRole> ToolRoles => Set<ToolRole>();
    public DbSet<ConsultantToolRole> ConsultantToolRoles => Set<ConsultantToolRole>();
    public DbSet<ProjectStep> ProjectSteps => Set<ProjectStep>();



    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        
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