using System.Text.Json;
using Backend.Modules.Events.Handlers;
using Backend.Modules.Events.Models;
using Backend.Modules.Events.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Backend.Tests.Events;

[Collection("WorkflowConfigFile")]
public class EventProcessorServiceTests
{
    private static WorkflowRulesService CreateRulesService()
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "workflow-config.json");
        File.WriteAllText(configPath, """
        {
          "workflowRules": [
            { "eventCode": "OrderCreated", "actionType": "ACTION_A", "targetType": "ROLE" },
            { "eventCode": "OrderCreated", "actionType": "ACTION_B", "targetType": "ROLE" }
          ]
        }
        """);
        return new WorkflowRulesService(NullLogger<WorkflowRulesService>.Instance);
    }

    [Fact]
    public async Task ProcessAsync_WithInvalidJson_LogsWarningAndDoesNotThrow()
    {
        var rulesService = CreateRulesService();
        var processor = new EventProcessorService(NullLogger<EventProcessorService>.Instance, rulesService, Array.Empty<IActionHandler>());

        var act = () => processor.ProcessAsync("null");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProcessAsync_WithNoMatchingRules_DoesNotInvokeAnyHandler()
    {
        var rulesService = CreateRulesService();
        var handler = new Mock<IActionHandler>();
        handler.Setup(h => h.ActionType).Returns("ACTION_A");
        var processor = new EventProcessorService(NullLogger<EventProcessorService>.Instance, rulesService, new[] { handler.Object });

        await processor.ProcessAsync(JsonSerializer.Serialize(new { eventType = "UnknownEvent" }));

        handler.Verify(h => h.HandleAsync(It.IsAny<WorkflowRule>(), It.IsAny<AcpEventDto>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_WithMatchingRuleButNoHandlerRegistered_DoesNotThrow()
    {
        var rulesService = CreateRulesService();
        var processor = new EventProcessorService(NullLogger<EventProcessorService>.Instance, rulesService, Array.Empty<IActionHandler>());

        var act = () => processor.ProcessAsync(JsonSerializer.Serialize(new { eventType = "OrderCreated" }));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProcessAsync_WithMultipleMatchingRules_InvokesEachRegisteredHandler()
    {
        var rulesService = CreateRulesService();
        var handlerA = new Mock<IActionHandler>();
        handlerA.Setup(h => h.ActionType).Returns("ACTION_A");
        var handlerB = new Mock<IActionHandler>();
        handlerB.Setup(h => h.ActionType).Returns("ACTION_B");
        var processor = new EventProcessorService(
            NullLogger<EventProcessorService>.Instance, rulesService, new[] { handlerA.Object, handlerB.Object });
        var projectId = Guid.NewGuid();

        await processor.ProcessAsync(JsonSerializer.Serialize(new { eventType = "OrderCreated" }), projectId);

        handlerA.Verify(h => h.HandleAsync(It.IsAny<WorkflowRule>(), It.IsAny<AcpEventDto>(), projectId), Times.Once);
        handlerB.Verify(h => h.HandleAsync(It.IsAny<WorkflowRule>(), It.IsAny<AcpEventDto>(), projectId), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WhenStreamIdProvidedButEventHasNone_SetsStreamIdOnDto()
    {
        var rulesService = CreateRulesService();
        AcpEventDto? capturedDto = null;
        var handlerA = new Mock<IActionHandler>();
        handlerA.Setup(h => h.ActionType).Returns("ACTION_A");
        handlerA.Setup(h => h.HandleAsync(It.IsAny<WorkflowRule>(), It.IsAny<AcpEventDto>(), It.IsAny<Guid?>()))
            .Callback<WorkflowRule, AcpEventDto, Guid?>((_, dto, _) => capturedDto = dto)
            .Returns(Task.CompletedTask);
        var processor = new EventProcessorService(NullLogger<EventProcessorService>.Instance, rulesService, new[] { handlerA.Object });
        var streamId = Guid.NewGuid();

        await processor.ProcessAsync(JsonSerializer.Serialize(new { eventType = "OrderCreated" }), streamId: streamId);

        capturedDto.Should().NotBeNull();
        capturedDto!.StreamId.Should().Be(streamId);
    }
}
