using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Planning.Services;
using Backend.Modules.Planning.Tools;
using Backend.Modules.Tools.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;

namespace Backend.Tests.Planning;

public class FsdPlanningServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IConfiguration CreateConfig(int maxRetries = 2, int retryDelayMs = 0) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Planning:MaxRetries"] = maxRetries.ToString(),
                ["Planning:RetryDelayMs"] = retryDelayMs.ToString()
            })
            .Build();

    private static Kernel CreateKernel(Mock<IChatCompletionService> chatMock)
    {
        chatMock.Setup(c => c.Attributes).Returns(new Dictionary<string, object?>());
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatMock.Object);
        return builder.Build();
    }

    private static IReadOnlyList<ChatMessageContent> AssistantReply(string content) =>
        new List<ChatMessageContent> { new(AuthorRole.Assistant, content) };

    private static FsdPlanningService CreateService(Kernel kernel, IConfiguration? config = null)
    {
        using var db = CreateDb();
        var tools = new PlanningTools(db, new PluginRegistry(db));
        return new FsdPlanningService(kernel, db, tools, config ?? CreateConfig(), NullLogger<FsdPlanningService>.Instance);
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnCleanedJson_WhenChatServiceReturnsMarkdownWrappedJson()
    {
        // Arrange
        var chatMock = new Mock<IChatCompletionService>();
        chatMock
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssistantReply("```json\n{\"streams\":[]}\n```"));
        var kernel = CreateKernel(chatMock);
        var service = CreateService(kernel);

        // Act
        var result = await service.GenerateAsync("some fsd text", "some guidelines");

        // Assert
        result.Should().Be("{\"streams\":[]}");
    }

    [Fact]
    public async Task GenerateAsync_ShouldRetry_WhenFirstAttemptReturnsEmpty()
    {
        // Arrange
        var chatMock = new Mock<IChatCompletionService>();
        chatMock
            .SetupSequence(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssistantReply(""))
            .ReturnsAsync(AssistantReply("{\"streams\":[]}"));
        var kernel = CreateKernel(chatMock);
        var service = CreateService(kernel, CreateConfig(maxRetries: 2, retryDelayMs: 0));

        // Act
        var result = await service.GenerateAsync("some fsd text", "some guidelines");

        // Assert
        result.Should().Be("{\"streams\":[]}");
        chatMock.Verify(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnNull_WhenAllAttemptsReturnEmpty()
    {
        // Arrange
        var chatMock = new Mock<IChatCompletionService>();
        chatMock
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssistantReply("   "));
        var kernel = CreateKernel(chatMock);
        var service = CreateService(kernel, CreateConfig(maxRetries: 2, retryDelayMs: 0));

        // Act
        var result = await service.GenerateAsync("some fsd text", "some guidelines");

        // Assert
        result.Should().BeNull();
        chatMock.Verify(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturnNull_WhenChatServiceThrowsOnEveryAttempt()
    {
        // Arrange
        var chatMock = new Mock<IChatCompletionService>();
        chatMock
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var kernel = CreateKernel(chatMock);
        var service = CreateService(kernel, CreateConfig(maxRetries: 2, retryDelayMs: 0));

        // Act
        var result = await service.GenerateAsync("some fsd text", "some guidelines");

        // Assert
        result.Should().BeNull();
        chatMock.Verify(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RefineAsync_ShouldReturnCleanedJson()
    {
        // Arrange
        var chatMock = new Mock<IChatCompletionService>();
        chatMock
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssistantReply("{\"streams\":[{\"name\":\"Updated\"}]}"));
        var kernel = CreateKernel(chatMock);
        var service = CreateService(kernel);

        // Act
        var result = await service.RefineAsync(
            currentPlan: "{\"streams\":[]}",
            conversation: "[]",
            userMessage: "add a stream",
            pluginsList: "- ID:plugin-1 | Name:Plugin",
            peopleContext: "Business Leads: none");

        // Assert
        result.Should().Be("{\"streams\":[{\"name\":\"Updated\"}]}");
    }
}
