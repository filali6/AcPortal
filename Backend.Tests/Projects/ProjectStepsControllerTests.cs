using System.Security.Claims;
using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Events.Services;
using Backend.Modules.Projects.Controllers;
using Backend.Modules.Projects.Models;
using Dapr.Client;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ProjectStream = Backend.Modules.Projects.Models.Stream;

namespace Backend.Tests.Projects;

public class ProjectStepsControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ProjectStepsController _controller;

    public ProjectStepsControllerTests()
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
        _controller = new ProjectStepsController(_db, eventPublisher);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void SetUser(ProjectStepsController controller, string keycloakId)
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
    public async Task CreateSteps_PersistsStepsAndResolvesDependencies()
    {
        var project = await AddProjectAsync();
        var lead = new User { FullName = "Lead", Email = "l@test.com", KeycloakId = "kc-lead", Role = GlobalRole.BusinessTeamLead };
        _db.Users.Add(lead);
        await _db.SaveChangesAsync();
        SetUser(_controller, lead.KeycloakId);

        var request = new CreateStepsRequest
        {
            ProjectId = project.Id,
            Steps = new List<StepDto>
            {
                new() { StepName = "Step1", ToolName = "Tool1", Order = 1 },
                new() { StepName = "Step2", ToolName = "Tool2", Order = 2, DependsOnStepId = "Step1" }
            }
        };

        var result = await _controller.CreateSteps(request);

        result.Should().BeOfType<OkObjectResult>();
        var steps = await _db.ProjectSteps.OrderBy(s => s.Order).ToListAsync();
        steps.Should().HaveCount(2);
        steps[1].DependsOnStepId.Should().Be(steps[0].Id);
    }

    [Fact]
    public async Task GetByProject_ReturnsStepsOrderedByOrder()
    {
        var project = await AddProjectAsync();
        _db.ProjectSteps.Add(new ProjectStep { ProjectId = project.Id, StepName = "B", Order = 2 });
        _db.ProjectSteps.Add(new ProjectStep { ProjectId = project.Id, StepName = "A", Order = 1 });
        await _db.SaveChangesAsync();

        var result = await _controller.GetByProject(project.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var steps = (IEnumerable<object>)ok.Value!;
        steps.Select(s => s.GetType().GetProperty("StepName")!.GetValue(s)).Should().Equal("A", "B");
    }

    [Fact]
    public async Task GetAiSteps_ReturnsNotFound_WhenStreamMissing()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await _controller.GetAiSteps(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetAiSteps_ReturnsStepsForBusinessLead()
    {
        var project = await AddProjectAsync();
        var lead = new User { FullName = "Lead", Email = "l@test.com", KeycloakId = "kc-lead" };
        _db.Users.Add(lead);
        var stream = new ProjectStream { Name = "S1", ProjectId = project.Id, BusinessTeamLeadId = lead.Id };
        _db.Streams.Add(stream);
        await _db.SaveChangesAsync();
        _db.ProjectSteps.Add(new ProjectStep { ProjectId = project.Id, StreamId = stream.Id, StepName = "Step1", TeamType = TeamType.Business });
        _db.ProjectSteps.Add(new ProjectStep { ProjectId = project.Id, StreamId = stream.Id, StepName = "Step2", TeamType = TeamType.Technical });
        await _db.SaveChangesAsync();
        SetUser(_controller, lead.KeycloakId);

        var result = await _controller.GetAiSteps(stream.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((IEnumerable<object>)ok.Value!).Should().ContainSingle();
    }

    [Fact]
    public async Task ApproveAiSteps_ReturnsNotFound_WhenStreamMissing()
    {
        var result = await _controller.ApproveAiSteps(Guid.NewGuid(), new ProjectStepsController.ApproveAiStepsRequest());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ApproveAiSteps_ReplacesStepsForMatchingTeamType()
    {
        var project = await AddProjectAsync();
        var lead = new User { FullName = "Lead", Email = "l@test.com", KeycloakId = "kc-lead" };
        _db.Users.Add(lead);
        var stream = new ProjectStream { Name = "S1", ProjectId = project.Id, BusinessTeamLeadId = lead.Id };
        _db.Streams.Add(stream);
        await _db.SaveChangesAsync();
        _db.ProjectSteps.Add(new ProjectStep { ProjectId = project.Id, StreamId = stream.Id, StepName = "Old", TeamType = TeamType.Business });
        await _db.SaveChangesAsync();
        SetUser(_controller, lead.KeycloakId);

        var request = new ProjectStepsController.ApproveAiStepsRequest
        {
            Steps = new List<StepDto> { new() { StepName = "New", ToolName = "Tool", Order = 1 } }
        };

        var result = await _controller.ApproveAiSteps(stream.Id, request);

        result.Should().BeOfType<OkObjectResult>();
        var steps = await _db.ProjectSteps.Where(s => s.StreamId == stream.Id).ToListAsync();
        steps.Should().ContainSingle(s => s.StepName == "New");
    }

    [Fact]
    public async Task ApproveAiSteps_KeepsExistingSteps_WhenNoStepsProvided()
    {
        var project = await AddProjectAsync();
        var stream = new ProjectStream { Name = "S1", ProjectId = project.Id };
        _db.Streams.Add(stream);
        _db.ProjectSteps.Add(new ProjectStep { ProjectId = project.Id, StreamId = stream.Id, StepName = "Old", TeamType = TeamType.Business });
        await _db.SaveChangesAsync();
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await _controller.ApproveAiSteps(stream.Id, new ProjectStepsController.ApproveAiStepsRequest());

        result.Should().BeOfType<OkObjectResult>();
        (await _db.ProjectSteps.CountAsync()).Should().Be(1);
    }
}
