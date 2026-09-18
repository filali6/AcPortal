using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Events.Handlers;
using Backend.Modules.Events.Models;
using Backend.Modules.Notifications.Services;
using Backend.Modules.Projects.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ProjectStream = Backend.Modules.Projects.Models.Stream;

namespace Backend.Tests.Events;

public class SendCommentEmailHandlerTests : IDisposable
{
    private readonly AppDbContext _db;

    public SendCommentEmailHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private SendCommentEmailHandler CreateHandler(ILogger<SendCommentEmailHandler>? logger = null) =>
        new(_db,
            new EmailService(new ConfigurationBuilder().Build(), NullLogger<EmailService>.Instance),
            logger ?? NullLogger<SendCommentEmailHandler>.Instance);

    [Fact]
    public async Task HandleAsync_NoOp_WhenStreamIdMissing()
    {
        var handler = CreateHandler();

        var act = async () => await handler.HandleAsync(new WorkflowRule(), new AcpEventDto { StreamId = null, AuthorName = "Alice" }, null);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_NoOp_WhenAuthorNameMissing()
    {
        var handler = CreateHandler();

        var act = async () => await handler.HandleAsync(new WorkflowRule(), new AcpEventDto { StreamId = Guid.NewGuid(), AuthorName = null }, null);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_NoOp_WhenStreamNotFound()
    {
        var handler = CreateHandler();

        var act = async () => await handler.HandleAsync(
            new WorkflowRule(), new AcpEventDto { StreamId = Guid.NewGuid(), AuthorName = "Alice" }, null);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HandleAsync_LogsRecipientCount_ForDistinctTeamLeads()
    {
        var businessLead = new User { FullName = "Business Lead", Email = "lead-b@test.com", KeycloakId = "kc-b" };
        var technicalLead = new User { FullName = "Technical Lead", Email = "lead-t@test.com", KeycloakId = "kc-t" };
        _db.Users.AddRange(businessLead, technicalLead);
        var project = new Project { Name = "P" };
        _db.Projects.Add(project);
        var stream = new ProjectStream { Name = "S", Project = project, BusinessTeamLead = businessLead, TechnicalTeamLead = technicalLead };
        _db.Streams.Add(stream);
        await _db.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<SendCommentEmailHandler>>();
        var handler = CreateHandler(loggerMock.Object);

        await handler.HandleAsync(
            new WorkflowRule(),
            new AcpEventDto { StreamId = stream.Id, AuthorName = "Someone Else", TaskTitle = "Task", Content = "Hi" },
            null);

        loggerMock.Verify(l => l.Log(
            LogLevel.Information,
            It.IsAny<EventId>(),
            It.IsAny<It.IsAnyType>(),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
