using System.Security.Claims;
using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Events.Services;
using Backend.Modules.Git.Models;
using Backend.Modules.Git.Services;
using Backend.Modules.Projects.Controllers;
using Backend.Modules.Projects.Models;
using Dapr.Client;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ProjectStream = Backend.Modules.Projects.Models.Stream;

namespace Backend.Tests.Projects;

public class StreamControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Mock<IGitProvider> _gitProvider;
    private readonly StreamController _controller;

    public StreamControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventPublisher:MaxRetries"] = "0"
        }).Build();
        var eventPublisher = new EventPublisher(new DaprClientBuilder().Build(), NullLogger<EventPublisher>.Instance, config);
        _gitProvider = new Mock<IGitProvider>();
        var gitService = new GitService(_gitProvider.Object, _db, NullLogger<GitService>.Instance);
        _controller = new StreamController(_db, eventPublisher, gitService);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void SetUser(StreamController controller, string keycloakId)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, keycloakId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private async Task<Project> AddProjectAsync()
    {
        var project = new Project { Name = "Project X" };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return project;
    }

    [Fact]
    public async Task Create_ReturnsOk_PersistsStreamMembersAndInitsRepo()
    {
        var project = await AddProjectAsync();
        var businessConsultant = Guid.NewGuid();
        var technicalConsultant = Guid.NewGuid();
        _gitProvider
            .Setup(p => p.CreateRepoAsync(It.IsAny<Guid>(), "S1", project.Id))
            .ReturnsAsync("https://git.example.com/s1.git");

        var result = await _controller.Create(new CreateStreamDto
        {
            Name = "S1",
            ProjectId = project.Id,
            BusinessTeamConsultants = new List<Guid> { businessConsultant },
            TechnicalTeamConsultants = new List<Guid> { technicalConsultant }
        });

        result.Should().BeOfType<OkObjectResult>();
        var stream = await _db.Streams.SingleAsync();
        stream.GitRepoUrl.Should().Be("https://git.example.com/s1.git");
        (await _db.StreamMembers.CountAsync()).Should().Be(2);
        _gitProvider.Verify(p => p.CreateRepoAsync(stream.Id, "S1", project.Id), Times.Once);
    }

    [Fact]
    public async Task GetByProject_ReturnsStreamsForThatProject()
    {
        var project = await AddProjectAsync();
        var consultant = new User { FullName = "C1", Email = "c1@test.com", KeycloakId = "kc-c1" };
        _db.Users.Add(consultant);
        var stream = new ProjectStream { Name = "S1", ProjectId = project.Id };
        _db.Streams.Add(stream);
        await _db.SaveChangesAsync();
        _db.StreamMembers.Add(new StreamMember { StreamId = stream.Id, ConsultantId = consultant.Id, TeamType = TeamType.Business });
        await _db.SaveChangesAsync();

        var result = await _controller.GetByProject(project.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().ContainSingle();
    }

    [Fact]
    public async Task AddMember_ReturnsOk_AddsMember()
    {
        var stream = new ProjectStream { Name = "S1", ProjectId = Guid.NewGuid() };
        _db.Streams.Add(stream);
        await _db.SaveChangesAsync();

        var result = await _controller.AddMember(stream.Id, new AddMemberDto { ConsultantId = Guid.NewGuid(), TeamType = TeamType.Technical });

        result.Should().BeOfType<OkObjectResult>();
        (await _db.StreamMembers.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetMyStreams_ReturnsUnauthorized_WhenNoClaim()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await _controller.GetMyStreams();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetMyStreams_ReturnsNotFound_WhenUserMissing()
    {
        SetUser(_controller, "unknown-kc");

        var result = await _controller.GetMyStreams();

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMyStreams_ReturnsOk_WithStreamsForLeadOrMember()
    {
        var project = await AddProjectAsync();
        var lead = new User { FullName = "Lead", Email = "lead@test.com", KeycloakId = "kc-lead" };
        _db.Users.Add(lead);
        var stream = new ProjectStream { Name = "S1", ProjectId = project.Id, BusinessTeamLeadId = lead.Id };
        _db.Streams.Add(stream);
        await _db.SaveChangesAsync();
        SetUser(_controller, lead.KeycloakId);

        var result = await _controller.GetMyStreams();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().ContainSingle();
    }

    [Fact]
    public async Task RemoveMember_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.RemoveMember(Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task RemoveMember_ReturnsOk_RemovesMember()
    {
        var stream = new ProjectStream { Name = "S1", ProjectId = Guid.NewGuid() };
        _db.Streams.Add(stream);
        var consultantId = Guid.NewGuid();
        _db.StreamMembers.Add(new StreamMember { StreamId = stream.Id, ConsultantId = consultantId, TeamType = TeamType.Business });
        await _db.SaveChangesAsync();

        var result = await _controller.RemoveMember(stream.Id, consultantId);

        result.Should().BeOfType<OkResult>();
        (await _db.StreamMembers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UpdateLeads_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.UpdateLeads(Guid.NewGuid(), new StreamController.UpdateLeadsDto());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task UpdateLeads_ReturnsOk_UpdatesLeadIds()
    {
        var stream = new ProjectStream { Name = "S1", ProjectId = Guid.NewGuid() };
        _db.Streams.Add(stream);
        await _db.SaveChangesAsync();
        var businessLeadId = Guid.NewGuid();
        var technicalLeadId = Guid.NewGuid();

        var result = await _controller.UpdateLeads(stream.Id, new StreamController.UpdateLeadsDto
        {
            BusinessTeamLeadId = businessLeadId,
            TechnicalTeamLeadId = technicalLeadId
        });

        result.Should().BeOfType<OkObjectResult>();
        var updated = await _db.Streams.FindAsync(stream.Id);
        updated!.BusinessTeamLeadId.Should().Be(businessLeadId);
        updated.TechnicalTeamLeadId.Should().Be(technicalLeadId);
    }

    [Fact]
    public async Task GetMembers_ReturnsNotFound_WhenMissing()
    {
        var result = await _controller.GetMembers(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMembers_ReturnsOk_WithLeadsAndMembers()
    {
        var businessLead = new User { FullName = "BLead", Email = "b@test.com", KeycloakId = "kc-b" };
        var technicalLead = new User { FullName = "TLead", Email = "t@test.com", KeycloakId = "kc-t" };
        var member = new User { FullName = "Member", Email = "m@test.com", KeycloakId = "kc-m" };
        _db.Users.AddRange(businessLead, technicalLead, member);
        var stream = new ProjectStream
        {
            Name = "S1",
            ProjectId = Guid.NewGuid(),
            BusinessTeamLeadId = businessLead.Id,
            TechnicalTeamLeadId = technicalLead.Id
        };
        _db.Streams.Add(stream);
        await _db.SaveChangesAsync();
        _db.StreamMembers.Add(new StreamMember { StreamId = stream.Id, ConsultantId = member.Id, TeamType = TeamType.Business });
        await _db.SaveChangesAsync();

        var result = await _controller.GetMembers(stream.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().HaveCount(3);
    }
}
