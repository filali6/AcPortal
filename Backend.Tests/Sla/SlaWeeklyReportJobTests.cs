using Backend.Modules.Sla.Jobs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Backend.Tests.Sla;

// SlaWeeklyReportJob schedules its work with a hardcoded 30s startup delay and a 7-day
// PeriodicTimer, so the report-generation branch can't be reached in a fast unit test.
// These tests cover the reachable behaviour: safe startup/shutdown of the BackgroundService.
public class SlaWeeklyReportJobTests
{
    [Fact]
    public async Task StopAsync_ShouldCompleteGracefully_WhenCancelledDuringInitialDelay()
    {
        var job = new SlaWeeklyReportJob(Mock.Of<IServiceProvider>(), NullLogger<SlaWeeklyReportJob>.Instance);

        await job.StartAsync(CancellationToken.None);
        await job.StopAsync(CancellationToken.None);

        job.ExecuteTask.Should().NotBeNull();
        job.ExecuteTask!.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task StopAsync_ShouldNotThrow_EvenThoughInitialDelayIsCancelled()
    {
        var job = new SlaWeeklyReportJob(Mock.Of<IServiceProvider>(), NullLogger<SlaWeeklyReportJob>.Instance);

        await job.StartAsync(CancellationToken.None);

        var act = async () => await job.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }
}
