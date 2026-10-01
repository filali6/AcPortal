using Backend.Data;
using Backend.Modules.Sla.Jobs;
using Backend.Modules.Sla.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Backend.Tests.Sla;

// The internal scheduling decisions are exercised directly to avoid waiting for the
// job's startup delay and weekly timer in unit tests.
public class SlaWeeklyReportJobTests
{
    [Fact]
    public async Task StopAsync_ShouldCompleteGracefully_WhenCancelledDuringInitialDelay()
    {
        var job = new SlaWeeklyReportJob(
            Mock.Of<IServiceProvider>(), new ConfigurationBuilder().Build(),
            NullLogger<SlaWeeklyReportJob>.Instance);

        await job.StartAsync(CancellationToken.None);
        await job.StopAsync(CancellationToken.None);

        job.ExecuteTask.Should().NotBeNull();
        job.ExecuteTask!.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_ShouldNotThrow_EvenThoughInitialDelayIsCancelled()
    {
        var job = new SlaWeeklyReportJob(
            Mock.Of<IServiceProvider>(), new ConfigurationBuilder().Build(),
            NullLogger<SlaWeeklyReportJob>.Instance);

        await job.StartAsync(CancellationToken.None);

        var act = async () => await job.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("2026-09-28T07:00:00Z", "2026-09-28T08:00:00Z")]
    [InlineData("2026-09-28T08:00:00Z", "2026-10-05T08:00:00Z")]
    [InlineData("2026-09-30T12:00:00Z", "2026-10-05T08:00:00Z")]
    public void NextRunUtc_ReturnsNextMondayAtConfiguredHour(string nowText, string expectedText)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sla:WeeklyReportHourUtc"] = "8"
        }).Build();
        var job = new SlaWeeklyReportJob(
            Mock.Of<IServiceProvider>(), config, NullLogger<SlaWeeklyReportJob>.Instance);
        var nextRun = typeof(SlaWeeklyReportJob).GetMethod(
            "NextRunUtc", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        var actual = (DateTime)nextRun.Invoke(job, new object[] { DateTime.Parse(nowText).ToUniversalTime() })!;

        actual.Should().Be(DateTime.Parse(expectedText).ToUniversalTime());
    }

    [Fact]
    public async Task GenerateIfDueAsync_DoesNotGenerateDuplicateAutomaticReport()
    {
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));
        using var provider = services.BuildServiceProvider();
        var weekStart = SlaWeeklyReport.WeekStartOf(DateTime.UtcNow);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SlaWeeklyReports.Add(new SlaWeeklyReport { WeekStart = weekStart, IsManual = false });
            await db.SaveChangesAsync();
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sla:WeeklyReportHourUtc"] = "0"
        }).Build();
        var job = new SlaWeeklyReportJob(provider, config, NullLogger<SlaWeeklyReportJob>.Instance);
        var generateIfDue = typeof(SlaWeeklyReportJob).GetMethod(
            "GenerateIfDueAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        var invocation = (Task)generateIfDue.Invoke(job, null)!;
        await invocation;

        using var verifyScope = provider.CreateScope();
        var reports = await verifyScope.ServiceProvider.GetRequiredService<AppDbContext>().SlaWeeklyReports.ToListAsync();
        reports.Should().ContainSingle();
    }
}
