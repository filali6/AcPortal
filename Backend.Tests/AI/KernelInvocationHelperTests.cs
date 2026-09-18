using Backend.Modules.AI.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Xunit;

namespace Backend.Tests.AI;

public class KernelInvocationHelperTests
{
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

    private static KernelInvocationHelper CreateHelper(IConfiguration? config = null) =>
        new(config ?? CreateConfig(), NullLogger<KernelInvocationHelper>.Instance);

    [Fact]
    public async Task InvokeAsync_StripsCodeFences_FromKernelResponse()
    {
        var chatMock = new Mock<IChatCompletionService>();
        chatMock
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssistantReply("```json\n{\"ok\":true}\n```"));
        var kernel = CreateKernel(chatMock);
        var helper = CreateHelper();

        var result = await helper.InvokeAsync(kernel, "prompt");

        result.Should().Be("{\"ok\":true}");
    }

    [Fact]
    public async Task InvokeAsync_ReturnsRawResult_WhenNoCodeFences()
    {
        var chatMock = new Mock<IChatCompletionService>();
        chatMock
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssistantReply("  plain text result  "));
        var kernel = CreateKernel(chatMock);
        var helper = CreateHelper();

        var result = await helper.InvokeAsync(kernel, "prompt");

        result.Should().Be("plain text result");
    }

    [Fact]
    public async Task InvokeAsync_ReturnsNull_WhenServiceThrowsOnEveryAttempt()
    {
        var chatMock = new Mock<IChatCompletionService>();
        chatMock
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var kernel = CreateKernel(chatMock);
        var helper = CreateHelper(CreateConfig(maxRetries: 3, retryDelayMs: 0));

        var result = await helper.InvokeAsync(kernel, "prompt");

        result.Should().BeNull();
        chatMock.Verify(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task InvokeAsync_Retries_WhenFirstAttemptsReturnEmptyResult()
    {
        var chatMock = new Mock<IChatCompletionService>();
        chatMock
            .SetupSequence(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssistantReply(""))
            .ReturnsAsync(AssistantReply("final result"));
        var kernel = CreateKernel(chatMock);
        var helper = CreateHelper(CreateConfig(maxRetries: 3, retryDelayMs: 0));

        var result = await helper.InvokeAsync(kernel, "prompt");

        result.Should().Be("final result");
        chatMock.Verify(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public void CleanJson_StripsTripleBacktickJsonFences()
    {
        var raw = "```json\nline1\nline2\n```";

        var result = KernelInvocationHelper.CleanJson(raw);

        result.Should().Be("line1\nline2");
    }

    [Fact]
    public void CleanJson_ReturnsTrimmedInput_WhenNoFencesPresent()
    {
        var raw = "  plain result  ";

        var result = KernelInvocationHelper.CleanJson(raw);

        result.Should().Be("plain result");
    }
}
