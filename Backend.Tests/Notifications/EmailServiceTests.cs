using Backend.Modules.Notifications.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Backend.Tests.Notifications;

// EmailService talks to a real SMTP server via MailKit with no injectable client, so these
// tests point it at an unreachable loopback endpoint and assert the failure is swallowed
// (logged, never thrown) rather than asserting on actual email delivery.
public class EmailServiceTests
{
    private static IConfiguration CreateConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Email:Host"] = "127.0.0.1",
                ["Email:Port"] = "1",
                ["Email:Username"] = "noreply@test.com",
                ["Email:Password"] = "pwd"
            })
            .Build();

    private static (EmailService service, Mock<ILogger<EmailService>> logger) CreateService()
    {
        var logger = new Mock<ILogger<EmailService>>();
        return (new EmailService(CreateConfig(), logger.Object), logger);
    }

    private static void VerifyErrorLogged(Mock<ILogger<EmailService>> logger) =>
        logger.Verify(l => l.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

    [Fact]
    public async Task SendMentionEmailAsync_DoesNotThrow_AndLogsError_WhenSmtpUnreachable()
    {
        var (service, logger) = CreateService();

        var act = async () => await service.SendMentionEmailAsync("to@test.com", "To", "Author", "Task", "comment");

        await act.Should().NotThrowAsync();
        VerifyErrorLogged(logger);
    }

    [Fact]
    public async Task SendStreamCreatedEmailAsync_DoesNotThrow_AndLogsError_WhenSmtpUnreachable()
    {
        var (service, logger) = CreateService();

        var act = async () => await service.SendStreamCreatedEmailAsync("to@test.com", "To", "Project", "Stream");

        await act.Should().NotThrowAsync();
        VerifyErrorLogged(logger);
    }

    [Fact]
    public async Task SendNewCommentEmailAsync_DoesNotThrow_AndLogsError_WhenSmtpUnreachable()
    {
        var (service, logger) = CreateService();

        var act = async () => await service.SendNewCommentEmailAsync("to@test.com", "To", "Author", "Task", "comment");

        await act.Should().NotThrowAsync();
        VerifyErrorLogged(logger);
    }

    [Fact]
    public async Task SendSlackReplyEmailAsync_DoesNotThrow_AndLogsError_WhenSmtpUnreachable()
    {
        var (service, logger) = CreateService();

        var act = async () => await service.SendSlackReplyEmailAsync("to@test.com", "To", "Replier", "Task", "content");

        await act.Should().NotThrowAsync();
        VerifyErrorLogged(logger);
    }

    [Fact]
    public async Task SendSlackChannelMessageEmailAsync_DoesNotThrow_AndLogsError_WhenSmtpUnreachable()
    {
        var (service, logger) = CreateService();

        var act = async () => await service.SendSlackChannelMessageEmailAsync("to@test.com", "To", "Author", "Stream", "content");

        await act.Should().NotThrowAsync();
        VerifyErrorLogged(logger);
    }
}
