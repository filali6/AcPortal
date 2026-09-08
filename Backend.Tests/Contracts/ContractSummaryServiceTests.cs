using Backend.Modules.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Xunit;

namespace Backend.Tests.Contracts;

public class ContractSummaryServiceTests
{
    // Fake IChatCompletionService so tests never call a real LLM provider.
    private class FakeChatCompletionService : IChatCompletionService
    {
        private readonly Func<ChatHistory, string> _respond;

        public FakeChatCompletionService(Func<ChatHistory, string> respond) => _respond = respond;

        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ChatMessageContent> result = new List<ChatMessageContent>
            {
                new(AuthorRole.Assistant, _respond(chatHistory))
            };
            return Task.FromResult(result);
        }

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    private static Kernel CreateKernel(Func<ChatHistory, string> respond)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(new FakeChatCompletionService(respond));
        return builder.Build();
    }

    [Fact]
    public async Task SummarizeAsync_ReturnsKernelResponseText()
    {
        var kernel = CreateKernel(_ => "This contract expires in 12 months.");
        var service = new ContractSummaryService(kernel);

        var result = await service.SummarizeAsync("Full contract text", "Summarize this contract:");

        result.Should().Be("This contract expires in 12 months.");
    }

    [Fact]
    public async Task SummarizeAsync_SendsPromptAndContractText_ToKernel()
    {
        // The fake echoes back whatever it received, so we can assert on the composed prompt.
        var kernel = CreateKernel(history => string.Join("\n", history.Select(m => m.Content)));
        var service = new ContractSummaryService(kernel);

        var result = await service.SummarizeAsync("CONTRACT BODY", "Summarize please");

        result.Should().Contain("Summarize please");
        result.Should().Contain("CONTRACT BODY");
    }
}
