using Backend.Data;
using Backend.Hubs;
using Backend.Modules.AI.Services;
using Backend.Modules.Notifications.Services;
using Backend.Modules.Sla.Controllers;
using Backend.Modules.Sla.Models;
using Backend.Modules.Sla.Services;
using Backend.Modules.Sla.Tools;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Xunit;

namespace Backend.Tests.Sla;

public class SlaAgentControllerTests : IDisposable
{
    private readonly AppDbContext _db;

    public SlaAgentControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static IConfiguration CreateConfig(int maxRetries = 1, int retryDelayMs = 0) =>
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

    private SlaAgentController CreateController(Kernel kernel, IConfiguration? config = null)
    {
        var slaChecker = new SlaCheckerService(_db, NullLogger<SlaCheckerService>.Instance);
        var tools = new SlaAgentTools(_db, slaChecker);
        var invocationHelper = new KernelInvocationHelper(config ?? CreateConfig(), NullLogger<KernelInvocationHelper>.Instance);
        var agent = new SlaAgentService(
            kernel,
            _db,
            slaChecker,
            CreateNotificationService(),
            tools,
            invocationHelper,
            NullLogger<SlaAgentService>.Instance);
        return new SlaAgentController(agent, _db, NullLogger<SlaAgentController>.Instance);
    }

    [Fact]
    public async Task GetRiskAnalysis_ReturnsOk_WhenAgentReturnsValidJson()
    {
        var chatMock = new Mock<IChatCompletionService>();
        chatMock
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssistantReply("{\"atRiskTasks\":[],\"recommendations\":[]}"));
        var controller = CreateController(CreateKernel(chatMock));

        var result = await controller.GetRiskAnalysis(Guid.NewGuid());

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetRiskAnalysis_Returns500_WhenAgentFailsOnEveryAttempt()
    {
        var chatMock = new Mock<IChatCompletionService>();
        chatMock
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var controller = CreateController(CreateKernel(chatMock), CreateConfig(maxRetries: 1));

        var result = await controller.GetRiskAnalysis(Guid.NewGuid());

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task GenerateWeeklyReport_ReturnsOk_WithReport()
    {
        var chatMock = new Mock<IChatCompletionService>();
        chatMock
            .Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssistantReply("## Weekly report"));
        var controller = CreateController(CreateKernel(chatMock));

        var result = await controller.GenerateWeeklyReport();

        result.Should().BeOfType<OkObjectResult>();
        (await _db.SlaWeeklyReports.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetLatestWeeklyReport_ReturnsNotFound_WhenNoneGenerated()
    {
        var chatMock = new Mock<IChatCompletionService>();
        var controller = CreateController(CreateKernel(chatMock));

        var result = await controller.GetLatestWeeklyReport();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetLatestWeeklyReport_ReturnsOk_WithMostRecentReport()
    {
        var chatMock = new Mock<IChatCompletionService>();
        var controller = CreateController(CreateKernel(chatMock));
        _db.SlaWeeklyReports.Add(new SlaWeeklyReport { Content = "Old", GeneratedAt = DateTime.UtcNow.AddDays(-7) });
        _db.SlaWeeklyReports.Add(new SlaWeeklyReport { Content = "New", GeneratedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var result = await controller.GetLatestWeeklyReport();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((SlaWeeklyReport)ok.Value!).Content.Should().Be("New");
    }
}
