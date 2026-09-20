using System.Reflection;
using Dapr.Messaging.PublishSubscribe;
using Xunit;
using Xunit.Abstractions;

namespace Backend.Tests.Events;

public class InspectTests
{
    private readonly ITestOutputHelper _output;
    public InspectTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Dump()
    {
        foreach (var ctor in typeof(DaprPublishSubscribeClient).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            _output.WriteLine(ctor.ToString());
        }
    }
}
