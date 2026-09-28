using Backend.Data;
using Backend.Modules.Sla.Models;
using Backend.Modules.Sla.Services;
using Microsoft.EntityFrameworkCore;

namespace Backend.Modules.Sla.Jobs;

// Job hebdomadaire — génère et notifie le rapport SLA (US77).
// Chaque LUNDI à l'heure configurée (UTC), UN SEUL rapport automatique par semaine :
//   - un redémarrage de l'application ne crée plus de doublon ni de notification en double
//   - si l'application était arrêtée le lundi, le rapport est rattrapé au démarrage suivant
public class SlaWeeklyReportJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SlaWeeklyReportJob> _logger;
    private readonly int _hourUtc;

    public SlaWeeklyReportJob(
        IServiceProvider serviceProvider,
        IConfiguration config,
        ILogger<SlaWeeklyReportJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _hourUtc = Math.Clamp(config.GetValue("Sla:WeeklyReportHourUtc", 8), 0, 23);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Petit délai au démarrage pour laisser l'application finir de s'initialiser
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateIfDueAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SlaWeeklyReportJob failed");
            }

            var delay = NextRunUtc(DateTime.UtcNow) - DateTime.UtcNow;
            _logger.LogInformation("SlaWeeklyReportJob: next run in {Delay}", delay);
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task GenerateIfDueAsync()
    {
        var now = DateTime.UtcNow;
        var weekStart = SlaWeeklyReport.WeekStartOf(now);

        // Pas encore l'heure cette semaine (ex : lundi 7h)
        if (now < weekStart.AddHours(_hourUtc)) return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var alreadyDone = await db.SlaWeeklyReports
            .AnyAsync(r => r.WeekStart == weekStart && !r.IsManual);
        if (alreadyDone) return;

        _logger.LogInformation("SlaWeeklyReportJob: generating weekly SLA report...");
        var agent = scope.ServiceProvider.GetRequiredService<SlaAgentService>();
        await agent.GenerateWeeklyReportAsync(isManual: false);
    }

    // Prochain lundi à _hourUtc (cette semaine si pas encore passé, sinon la semaine suivante)
    private DateTime NextRunUtc(DateTime now)
    {
        var thisWeek = SlaWeeklyReport.WeekStartOf(now).AddHours(_hourUtc);
        return now < thisWeek ? thisWeek : thisWeek.AddDays(7);
    }
}