using Backend.Modules.Sla.Services;

namespace Backend.Modules.Sla.Services;

 
public class SlaAutoApplyService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SlaAutoApplyService> _logger;
    private readonly TimeSpan _interval;

    public SlaAutoApplyService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<SlaAutoApplyService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(config.GetValue("Sla:AutoApplyIntervalMinutes", 15));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sla = scope.ServiceProvider.GetRequiredService<SlaCheckerService>();
                await sla.ApplySlaRulesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Automatic SLA application failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}