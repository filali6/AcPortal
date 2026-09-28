using Backend.Data;
using Backend.Modules.Sla.Models;
using Backend.Modules.Sla.Services;
using Backend.Modules.Tasks.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Backend.Tests.Sla;

public class SlaAutoApplyServiceTests
{
    [Fact]
    public async Task ExecuteAsync_AppliesRulesThroughScopedChecker()
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<Backend.Modules.Tools.Services.PluginRegistry>();
        services.AddScoped<Backend.Modules.Projects.Services.ProjectStatusService>();
        services.AddScoped<SlaCheckerService>();
        using var provider = services.BuildServiceProvider();

        AcpTask task;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rule = new SlaRule { Name = "Default", Type = SlaRuleType.Task, SlaDays = 4 };
            task = new AcpTask { Title = "Task without a due date", Status = AcpTaskStatus.Pending };
            db.SlaRules.Add(rule);
            db.AcpTasks.Add(task);
            await db.SaveChangesAsync();
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sla:AutoApplyIntervalMinutes"] = "60"
        }).Build();
        var service = new SlaAutoApplyService(
            provider.GetRequiredService<IServiceScopeFactory>(), config,
            NullLogger<SlaAutoApplyService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await using var verifyScope = provider.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!await verifyDb.AcpTasks.AsNoTracking().AnyAsync(t => t.Id == task.Id && t.DueDate != null))
            await Task.Delay(10, timeout.Token);
        await service.StopAsync(CancellationToken.None);

        var reloaded = await verifyDb.AcpTasks.SingleAsync(t => t.Id == task.Id);
        reloaded.DueDate.Should().BeCloseTo(task.CreatedAt.AddDays(4), TimeSpan.FromSeconds(1));
        reloaded.SlaRuleId.Should().NotBeNull();
    }
}