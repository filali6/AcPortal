using System.Diagnostics;
using System.Reflection;
using Backend.Modules.Events.Services;
using Dapr.Messaging.PublishSubscribe;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Dapr.Client;
using System.Collections.Concurrent;

namespace Backend.Tests.Events;

public class StreamingSubscriptionServiceTests
{
    // Moq can no longer proxy DaprPublishSubscribeClient (no accessible parameterless
    // constructor in the current Dapr.Messaging version), and these tests only exercise
    // the local polling logic in WaitForTopicAsync, which never touches the client.
    private static StreamingSubscriptionService CreateService()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        return new StreamingSubscriptionService(null!, new DaprClientBuilder().Build(), services, NullLogger<StreamingSubscriptionService>.Instance);
    }

    private static void MarkTopicReady(StreamingSubscriptionService service, string topic)
    {
        var field = typeof(StreamingSubscriptionService).GetField("_readyTopics", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var readyTopics = (System.Collections.Concurrent.ConcurrentDictionary<string, byte>)field.GetValue(service)!;
        readyTopics[topic] = 0;
    }

    [Fact]
    public async Task WaitForTopicAsync_WhenTopicAlreadyReady_ReturnsImmediately()
    {
        var service = CreateService();
        MarkTopicReady(service, "system.events");
        var stopwatch = Stopwatch.StartNew();

        await service.WaitForTopicAsync("system.events");

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500);
    }

    [Fact]
    public async Task WaitForTopicAsync_WhenTopicNeverBecomesReady_StopsPollingAfterTimeout()
    {
        var service = CreateService();
        var stopwatch = Stopwatch.StartNew();

        await service.WaitForTopicAsync("never-ready-topic");

        stopwatch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(2900);
    }
}
