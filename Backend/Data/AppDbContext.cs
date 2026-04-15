using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Events.Models;
using Backend.Modules.Tasks.Models;
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

    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Backend.Modules.Projects.Models.Stream> Streams => Set<Backend.Modules.Projects.Models.Stream>();
    public DbSet<StreamMember> StreamMembers => Set<StreamMember>();
    public DbSet<ProjectStep> ProjectSteps => Set<ProjectStep>();

    public DbSet<AcpTool> AcpTools => Set<AcpTool>();
    public DbSet<ToolRole> ToolRoles => Set<ToolRole>();
    public DbSet<ConsultantToolRole> ConsultantToolRoles => Set<ConsultantToolRole>();
    public DbSet<UserPlugin> UserPlugins => Set<UserPlugin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>()
            .HasIndex(u => u.KeycloakId).IsUnique();

        // AcpTask → AcpEvent
        modelBuilder.Entity<AcpTask>()
            .HasOne<AcpEvent>()
            .WithOne()
            .HasForeignKey<AcpTask>(t => t.SourceEventId);

        // Portfolio → PortfolioDirector
        modelBuilder.Entity<Portfolio>()
            .HasOne(p => p.PortfolioDirector)
            .WithMany()
            .HasForeignKey(p => p.PortfolioDirectorId)
            .OnDelete(DeleteBehavior.SetNull);

        // Project → Portfolio
        modelBuilder.Entity<Project>()
            .HasOne(p => p.Portfolio)
            .WithMany(pf => pf.Projects)
            .HasForeignKey(p => p.PortfolioId)
            .OnDelete(DeleteBehavior.SetNull);

        // Project → ProjectManager
        modelBuilder.Entity<Project>()
            .HasOne(p => p.ProjectManager)
            .WithMany()
            .HasForeignKey(p => p.ProjectManagerId)
            .OnDelete(DeleteBehavior.SetNull);

        // Stream → Project
        modelBuilder.Entity<Backend.Modules.Projects.Models.Stream>()
            .HasOne(s => s.Project)
            .WithMany(p => p.Streams)
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Stream → BusinessTeamLead
        modelBuilder.Entity<Backend.Modules.Projects.Models.Stream>()
            .HasOne(s => s.BusinessTeamLead)
            .WithMany()
            .HasForeignKey(s => s.BusinessTeamLeadId)
            .OnDelete(DeleteBehavior.SetNull);

        // Stream → TechnicalTeamLead
        modelBuilder.Entity<Backend.Modules.Projects.Models.Stream>()
            .HasOne(s => s.TechnicalTeamLead)
            .WithMany()
            .HasForeignKey(s => s.TechnicalTeamLeadId)
            .OnDelete(DeleteBehavior.SetNull);

        // StreamMember → Stream + Consultant
        modelBuilder.Entity<StreamMember>()
            .HasIndex(sm => new { sm.StreamId, sm.ConsultantId }).IsUnique();
        modelBuilder.Entity<StreamMember>()
            .HasOne(sm => sm.Stream)
            .WithMany(s => s.Members)
            .HasForeignKey(sm => sm.StreamId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<StreamMember>()
            .HasOne(sm => sm.Consultant)
            .WithMany()
            .HasForeignKey(sm => sm.ConsultantId)
            .OnDelete(DeleteBehavior.Cascade);

        // ProjectStep → Project + Stream
        modelBuilder.Entity<ProjectStep>()
            .HasOne(ps => ps.Project)
            .WithMany()
            .HasForeignKey(ps => ps.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProjectStep>()
            .HasOne(ps => ps.Stream)
            .WithMany()
            .HasForeignKey(ps => ps.StreamId)
            .OnDelete(DeleteBehavior.SetNull);

        // ToolRole → AcpTool
        modelBuilder.Entity<ToolRole>()
            .HasOne(tr => tr.Tool)
            .WithMany()
            .HasForeignKey(tr => tr.ToolId)
            .OnDelete(DeleteBehavior.Cascade);

        // ConsultantToolRole → User + AcpTool + ToolRole
        modelBuilder.Entity<ConsultantToolRole>()
            .HasOne(ctr => ctr.Consultant)
            .WithMany()
            .HasForeignKey(ctr => ctr.ConsultantId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ConsultantToolRole>()
            .HasOne(ctr => ctr.Tool)
            .WithMany()
            .HasForeignKey(ctr => ctr.ToolId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ConsultantToolRole>()
            .HasOne(ctr => ctr.ToolRole)
            .WithMany()
            .HasForeignKey(ctr => ctr.ToolRoleId)
            .OnDelete(DeleteBehavior.Cascade);
        // UserPlugin → User
        modelBuilder.Entity<UserPlugin>()
            .HasIndex(up => new { up.UserId, up.PluginId }).IsUnique();
    }
}