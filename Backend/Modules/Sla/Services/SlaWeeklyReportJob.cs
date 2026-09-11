using Backend.Modules.Sla.Services;

namespace Backend.Modules.Sla.Jobs;

// Job hebdomadaire — génère et notifie le rapport SLA (US77).
// Aucune dépendance externe (pas de Hangfire/Quartz) : un simple PeriodicTimer suffit
// pour ce besoin (une exécution par semaine, pas de scheduling complexe).
public class SlaWeeklyReportJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SlaWeeklyReportJob> _logger;

    public SlaWeeklyReportJob(IServiceProvider serviceProvider, ILogger<SlaWeeklyReportJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Petit délai au démarrage pour laisser l'application finir de s'initialiser
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromDays(7));

        do
        {
            try
            {
                _logger.LogInformation("SlaWeeklyReportJob: generating weekly SLA report...");

                // SlaAgentService (et ses dépendances comme AppDbContext) sont Scoped —
                // il faut créer un scope explicite depuis ce service Singleton.
                using var scope = _serviceProvider.CreateScope();
                var agent = scope.ServiceProvider.GetRequiredService<SlaAgentService>();
                await agent.GenerateWeeklyReportAsync();

                _logger.LogInformation("SlaWeeklyReportJob: report generated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SlaWeeklyReportJob failed");
            }
        }
        while (!stoppingToken.IsCancellationRequested
            && await timer.WaitForNextTickAsync(stoppingToken));
    }
}