using Backend.Data;
using Backend.Hubs;
using Backend.Modules.AI.Services;
using Backend.Modules.Auth.Models;
using Backend.Modules.Notifications.Services;
using Backend.Modules.Projects.Models;
using Backend.Modules.Sla.Models;
using Backend.Modules.Sla.Services;
using Backend.Modules.Sla.Tools;
using Backend.Modules.Tasks.Models;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;

namespace Backend.Tests.Sla;

public class SlaAgentServiceTests : IDisposable
{
    private readonly AppDbContext _db;

    public SlaAgentServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private static IConfiguration CreateConfig(int maxRetries = 2, int retryDelayMs = 0) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sla:MaxRetries"] = maxRetries.ToString(),
                ["Sla:RetryDelayMs"] = retryDelayMs.ToString()
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

    private NotificationService CreateNotificationService()
    {
        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(c => c.Group(It.IsAny<string>())).Returns(Mock.Of<IClientProxy>());
        var hub = new Mock<IHubContext<NotificationHub>>();
        hub.Setup(h => h.Clients).Returns(hubClients.Object);
        return new NotificationService(_db, hub.Object);
    }

    private SlaAgentService CreateService(Kernel kernel, IConfiguration? config = null)
    {
        var slaChecker = new SlaCheckerService(_db, NullLogger<SlaCheckerService>.Instance);
        var tools = new SlaAgentTools(_db, slaChecker);
        var invocationHelper = new KernelInvocationHelper(config ?? CreateConfig(), NullLogger<KernelInvocationHelper>.Instance);
        return new SlaAgentService(
            kernel,
            _db,
            slaChecker,
            CreateNotificationService(),
            tools,
            invocationHelper,
            NullLogger<SlaAgentService>.Instance);
    }

    // ── AnalyzeProjectRisksAsync ────────────────────────────────────

    [Fact]
    public async Task AnalyzeProjectRisksAsync_ShouldReturnCleanedJson_FromChatService()
    {
        var chatMock = new Mock<IChatCompletionService>();
        chatMock
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssistantReply("```json\n{\"atRiskTasks\":[],\"recommendations\":[]}\n```"));
        var kernel = CreateKernel(chatMock);
        var service = CreateService(kernel);

        var result = await service.AnalyzeProjectRisksAsync(Guid.NewGuid());

        result.Should().Be("{\"atRiskTasks\":[],\"recommendations\":[]}");
    }

    [Fact]
    public async Task AnalyzeProjectRisksAsync_ShouldReturnNull_WhenChatServiceThrowsOnEveryAttempt()
    {
        var chatMock = new Mock<IChatCompletionService>();
        chatMock
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var kernel = CreateKernel(chatMock);
        var service = CreateService(kernel, CreateConfig(maxRetries: 2, retryDelayMs: 0));

        var result = await service.AnalyzeProjectRisksAsync(Guid.NewGuid());

        result.Should().BeNull();
        chatMock.Verify(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ── GenerateWeeklyReportAsync ───────────────────────────────────

    private async Task<(User headOfCds, User consultant)> SeedUsersAsync()
    {
        var headOfCds = new User { FullName = "Head", Email = "head@x.com", KeycloakId = "kc-head", Role = GlobalRole.HeadOfCDS };
        var consultant = new User { FullName = "Consultant", Email = "cons@x.com", KeycloakId = "kc-cons", Role = GlobalRole.Consultant };
        _db.Users.AddRange(headOfCds, consultant);
        await _db.SaveChangesAsync();
        return (headOfCds, consultant);
    }

    private async Task SeedOverdueTaskAsync()
    {
        _db.AcpTasks.Add(new AcpTask
        {
            Title = "Overdue task",
            Status = AcpTaskStatus.Pending,
            DueDate = DateTime.UtcNow.AddDays(-2)
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task GenerateWeeklyReportAsync_ShouldPersistReport_WithCorrectCounts()
    {
        await SeedUsersAsync();
        await SeedOverdueTaskAsync();

        var chatMock = new Mock<IChatCompletionService>();
        chatMock
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssistantReply("## Weekly SLA Report\nAll good."));
        var kernel = CreateKernel(chatMock);
        var service = CreateService(kernel);

        var report = await service.GenerateWeeklyReportAsync();

        report.Content.Should().Be("## Weekly SLA Report\nAll good.");
        report.OverdueTasksCount.Should().Be(1);
        report.AtRiskTasksCount.Should().Be(0);
        (await _db.SlaWeeklyReports.FindAsync(report.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateWeeklyReportAsync_ShouldNotifyOnlyHeadOfCdsUsers()
    {
        var (headOfCds, consultant) = await SeedUsersAsync();
        await SeedOverdueTaskAsync();

        var chatMock = new Mock<IChatCompletionService>();
        chatMock
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssistantReply("report content"));
        var kernel = CreateKernel(chatMock);
        var service = CreateService(kernel);

        await service.GenerateWeeklyReportAsync();

        var notifications = await _db.Notifications.ToListAsync();
        notifications.Should().ContainSingle();
        notifications[0].RecipientKeycloakId.Should().Be(headOfCds.KeycloakId);
        notifications.Should().NotContain(n => n.RecipientKeycloakId == consultant.KeycloakId);
    }

    [Fact]
    public async Task GenerateWeeklyReportAsync_ShouldUseFallbackContent_WhenChatServiceFails()
    {
        var chatMock = new Mock<IChatCompletionService>();
        chatMock
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var kernel = CreateKernel(chatMock);
        var service = CreateService(kernel, CreateConfig(maxRetries: 1, retryDelayMs: 0));

        var report = await service.GenerateWeeklyReportAsync();

        report.Content.Should().Be("*Report generation failed this week — please retry manually.*");
        (await _db.SlaWeeklyReports.FindAsync(report.Id)).Should().NotBeNull();
    }
}
