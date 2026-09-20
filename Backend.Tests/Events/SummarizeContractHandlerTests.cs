using Backend.Data;
using Backend.Hubs;
using Backend.Modules.Auth.Models;
using Backend.Modules.Contracts.Models;
using Backend.Modules.Contracts.Services;
using Backend.Modules.Events.Handlers;
using Backend.Modules.Events.Models;
using Backend.Modules.Notifications.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Backend.Tests.Events;

public class SummarizeContractHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IPdfTextExtractor> _extractor = new();
    private readonly Mock<IContractSummaryService> _summaryService = new();
    private readonly NotificationService _notificationService;

    public SummarizeContractHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(c => c.Group(It.IsAny<string>())).Returns(Mock.Of<IClientProxy>());
        var hub = new Mock<IHubContext<NotificationHub>>();
        hub.Setup(h => h.Clients).Returns(hubClients.Object);
        _notificationService = new NotificationService(_db, hub.Object);
    }

   public void Dispose()
{
    _db.Dispose();
    GC.SuppressFinalize(this);
}

    private SummarizeContractHandler CreateHandler(IConfiguration? config = null)
    {
        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());

        return new SummarizeContractHandler(
            _db,
            _extractor.Object,
            _summaryService.Object,
            _notificationService,
            NullLogger<SummarizeContractHandler>.Instance,
            envMock.Object,
            config ?? new ConfigurationBuilder().Build());
    }

    [Fact]
    public async Task HandleAsync_NoOp_WhenContractIdMissing()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(new WorkflowRule(), new AcpEventDto { ContractId = null }, null);

        (await _db.Contracts.AnyAsync()).Should().BeFalse();
        _summaryService.Verify(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_NoOp_WhenContractNotFound()
    {
        var handler = CreateHandler();

        await handler.HandleAsync(new WorkflowRule(), new AcpEventDto { ContractId = Guid.NewGuid() }, null);

        _summaryService.Verify(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_MarksFailed_WhenContractHasNoPdfFiles()
    {
        var contract = new Contract { ClientName = "Acme", FilesPaths = new List<string>() };
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync();
        var handler = CreateHandler();

        await handler.HandleAsync(new WorkflowRule(), new AcpEventDto { ContractId = contract.Id }, null);

        (await _db.Contracts.FindAsync(contract.Id))!.SummaryStatus.Should().Be(SummaryStatus.Failed);
    }

    [Fact]
    public async Task HandleAsync_SummarizesSuccessfully_AndNotifiesDafUser()
    {
        var dafUser = new User { FullName = "Daf", Email = "daf@test.com", KeycloakId = "kc-daf" };
        _db.Users.Add(dafUser);
        var contract = new Contract { ClientName = "Acme", FilesPaths = new List<string> { "contract.pdf" }, DafUserId = dafUser.Id };
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync();

        _extractor.Setup(e => e.Extract(It.IsAny<string>())).Returns("extracted text");
        _summaryService.Setup(s => s.SummarizeAsync("extracted text", It.IsAny<string>())).ReturnsAsync("A short summary");

        var handler = CreateHandler();

        await handler.HandleAsync(new WorkflowRule(), new AcpEventDto { ContractId = contract.Id }, null);

        var updated = await _db.Contracts.FindAsync(contract.Id);
        updated!.SummaryStatus.Should().Be(SummaryStatus.Completed);
        updated.Summary.Should().Be("A short summary");
        updated.ExtractedText.Should().Be("extracted text");
        updated.SummarizedAt.Should().NotBeNull();

        var notification = await _db.Notifications.SingleAsync();
        notification.RecipientKeycloakId.Should().Be("kc-daf");
    }

    [Fact]
    public async Task HandleAsync_MarksFailed_WhenSummaryServiceThrowsOnEveryRetry()
    {
        var contract = new Contract { ClientName = "Acme", FilesPaths = new List<string> { "contract.pdf" } };
        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync();

        _extractor.Setup(e => e.Extract(It.IsAny<string>())).Returns("text");
        _summaryService.Setup(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("LLM down"));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SummarizeContract:MaxRetries"] = "1",
                ["SummarizeContract:RetryDelayMs"] = "0"
            })
            .Build();
        var handler = CreateHandler(config);

        await handler.HandleAsync(new WorkflowRule(), new AcpEventDto { ContractId = contract.Id }, null);

        (await _db.Contracts.FindAsync(contract.Id))!.SummaryStatus.Should().Be(SummaryStatus.Failed);
    }
}
