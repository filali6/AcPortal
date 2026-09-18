using Backend.Modules.Events.Services;
using Dapr.Client;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Backend.Tests.Events;

public class EventPublisherTests
{
    private static IConfiguration CreateConfig(int maxRetries, int retryDelayMs = 0) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EventPublisher:MaxRetries"] = maxRetries.ToString(),
                ["EventPublisher:RetryDelayMs"] = retryDelayMs.ToString()
            })
            .Build();

    private static EventPublisher CreatePublisher(IConfiguration config, Mock<ILogger<EventPublisher>> logger) =>
        new(new DaprClientBuilder().Build(), logger.Object, config);

    [Fact]
    public async Task PublishAsync_LogsError_WithoutRetrying_WhenMaxRetriesIsZero()
    {
        var logger = new Mock<ILogger<EventPublisher>>();
        var publisher = CreatePublisher(CreateConfig(maxRetries: 0), logger);

        await publisher.PublishAsync(new { message = "hello" });

        logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishAsync_RetriesConfiguredNumberOfTimes_AndLogsErrorAfterExhaustingRetries()
    {
        // No Dapr sidecar is running in the test environment, so every publish attempt
        // against localhost:3500 fails fast (connection refused), exercising the retry loop.
        var logger = new Mock<ILogger<EventPublisher>>();
        var publisher = CreatePublisher(CreateConfig(maxRetries: 2, retryDelayMs: 0), logger);

        await publisher.PublishAsync(new { message = "hello" });

        logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
        logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
