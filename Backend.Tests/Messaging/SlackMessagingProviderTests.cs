using Backend.Modules.Messaging.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SlackNet;
using SlackNet.WebApi;

namespace Backend.Tests.Messaging;

public class SlackMessagingProviderTests
{
    [Fact]
    public async Task CreateStreamChannelAsync_SanitizesNameAndReturnsNulls_WhenSlackFails()
    {
        var conversations = new Mock<IConversationsApi>();
        conversations
            .Setup(c => c.Create("proj-project-name-stream-1", false))
            .ThrowsAsync(new InvalidOperationException("Slack unavailable"));

        var slack = new Mock<ISlackApiClient>();
        slack.SetupGet(s => s.Conversations).Returns(conversations.Object);
        var provider = CreateProvider(slack.Object);

        var result = await provider.CreateStreamChannelAsync(
            "Project Name!", "Stream 1", new List<string>());

        result.Should().Be((null, null));
        conversations.Verify(c => c.Create("proj-project-name-stream-1", false), Times.Once);
    }

    [Fact]
    public async Task AddMemberAsync_SwallowsSlackFailure()
    {
        var users = new Mock<IUsersApi>();
        users.Setup(u => u.LookupByEmail("alice@test.com"))
            .ThrowsAsync(new InvalidOperationException("Slack unavailable"));

        var slack = new Mock<ISlackApiClient>();
        slack.SetupGet(s => s.Users).Returns(users.Object);
        var provider = CreateProvider(slack.Object);

        var action = () => provider.AddMemberAsync("C123", "alice@test.com");

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PostThreadMessageAsync_ReturnsNulls_WhenSlackFails()
    {
        var chat = new Mock<IChatApi>();
        chat.Setup(c => c.PostMessage(It.IsAny<SlackNet.WebApi.Message>()))
            .ThrowsAsync(new InvalidOperationException("Slack unavailable"));

        var slack = new Mock<ISlackApiClient>();
        slack.SetupGet(s => s.Chat).Returns(chat.Object);
        var provider = CreateProvider(slack.Object);

        var result = await provider.PostThreadMessageAsync("C123", "Hello");

        result.Should().Be((null, null));
    }

    [Fact]
    public async Task ReplyToThreadAsync_SwallowsSlackFailure()
    {
        var chat = new Mock<IChatApi>();
        chat.Setup(c => c.PostMessage(It.IsAny<SlackNet.WebApi.Message>()))
            .ThrowsAsync(new InvalidOperationException("Slack unavailable"));

        var slack = new Mock<ISlackApiClient>();
        slack.SetupGet(s => s.Chat).Returns(chat.Object);
        var provider = CreateProvider(slack.Object);

        var action = () => provider.ReplyToThreadAsync("C123", "171.42", "Reply");

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetUserNameAsync_ReturnsFallback_WhenSlackFails()
    {
        var users = new Mock<IUsersApi>();
        users.Setup(u => u.Info("U123"))
            .ThrowsAsync(new InvalidOperationException("Slack unavailable"));

        var slack = new Mock<ISlackApiClient>();
        slack.SetupGet(s => s.Users).Returns(users.Object);
        var provider = CreateProvider(slack.Object);

        var result = await provider.GetUserNameAsync("U123");

        result.Should().Be("Slack User");
    }

    private static SlackMessagingProvider CreateProvider(ISlackApiClient slack) =>
        new(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Messaging:Slack:BotToken"] = "test-token"
                })
                .Build(),
            NullLogger<SlackMessagingProvider>.Instance,
            slack);
}