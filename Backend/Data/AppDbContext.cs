using Backend.Modules.Events.Models;
using Backend.Modules.Tasks.Models;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // On explique à EF Core la relation entre les deux tables :
        // Un AcpEvent a une seule AcpTask (via SourceEventId)
        modelBuilder.Entity<AcpTask>()
            .HasOne<AcpEvent>()
            .WithOne()
            .HasForeignKey<AcpTask>(t => t.SourceEventId);
    }
}