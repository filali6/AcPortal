using System.Text.Json;
using Backend.Modules.Events.Controllers;
using Backend.Modules.Events.Handlers;
using Backend.Modules.Events.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Backend.Tests.Events;

[Collection("WorkflowConfigFile")]
public class EventsSubscriberControllerTests
{
    private static EventsSubscriberController CreateController(out Mock<IActionHandler> handler)
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "workflow-config.json");
        File.WriteAllText(configPath, """
        {
          "workflowRules": [
            { "eventCode": "TestEvent", "actionType": "TEST_ACTION", "targetType": "ROLE" }
          ]
        }
        """);
        var rulesService = new WorkflowRulesService(NullLogger<WorkflowRulesService>.Instance);
        handler = new Mock<IActionHandler>();
        handler.Setup(h => h.ActionType).Returns("TEST_ACTION");
        var processor = new EventProcessorService(NullLogger<EventProcessorService>.Instance, rulesService, new[] { handler.Object });
        return new EventsSubscriberController(processor, NullLogger<EventsSubscriberController>.Instance);
    }

    [Fact]
    public async Task HandleEvent_WithMatchingRule_InvokesHandler()
    {
        var controller = CreateController(out var handler);
        var payload = JsonDocument.Parse("""{"eventType":"TestEvent","projectId":"","streamId":""}""").RootElement;

        var result = await controller.HandleEvent(payload);

        result.Should().BeOfType<OkResult>();
        handler.Verify(h => h.HandleAsync(It.IsAny<Backend.Modules.Events.Models.WorkflowRule>(),
            It.IsAny<Backend.Modules.Events.Models.AcpEventDto>(), null), Times.Once);
    }

    [Fact]
    public async Task HandleEvent_WithProjectAndStreamIds_ParsesGuidsAndProcesses()
    {
        var controller = CreateController(out var handler);
        var projectId = Guid.NewGuid();
        var streamId = Guid.NewGuid();
        var payload = JsonDocument.Parse($$"""{"eventType":"TestEvent","projectId":"{{projectId}}","streamId":"{{streamId}}"}""").RootElement;

        var result = await controller.HandleEvent(payload);

        result.Should().BeOfType<OkResult>();
        handler.Verify(h => h.HandleAsync(It.IsAny<Backend.Modules.Events.Models.WorkflowRule>(),
            It.IsAny<Backend.Modules.Events.Models.AcpEventDto>(), projectId), Times.Once);
    }

    [Fact]
    public async Task HandleEvent_WithNoMatchingRule_DoesNotThrow()
    {
        var controller = CreateController(out var handler);
        var payload = JsonDocument.Parse("""{"eventType":"UnknownEvent"}""").RootElement;

        var result = await controller.HandleEvent(payload);

        result.Should().BeOfType<OkResult>();
        handler.Verify(h => h.HandleAsync(It.IsAny<Backend.Modules.Events.Models.WorkflowRule>(),
            It.IsAny<Backend.Modules.Events.Models.AcpEventDto>(), It.IsAny<Guid?>()), Times.Never);
    }
}
