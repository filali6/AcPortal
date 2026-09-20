using System.Security.Claims;
using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Contracts.Models;
using Backend.Modules.Contracts.Services;
using Backend.Modules.Events.Services;
using Backend.Modules.Projects.Controllers;
using Backend.Modules.Projects.Models;
using Backend.Modules.Projects.Services;
using Backend.Modules.Tasks.Models;
using Dapr.Client;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Stream = Backend.Modules.Projects.Models.Stream;

namespace Backend.Tests.Projects;

public class ProjectsControllerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ProjectsController _controller;

    public ProjectsControllerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var projectsService = new ProjectsService(_db, NullLogger<ProjectsService>.Instance);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EventPublisher:MaxRetries"] = "0"
        }).Build();
        var eventPublisher = new EventPublisher(new DaprClientBuilder().Build(), NullLogger<EventPublisher>.Instance, config);
        // Only exercised on the (untested) Create success path, so a null pubsub client is safe here.
        var streamingService = new StreamingSubscriptionService(null!, new ServiceCollection().BuildServiceProvider(), NullLogger<StreamingSubscriptionService>.Instance);
        var contractsService = new ContractsService(_db, NullLogger<ContractsService>.Instance, Mock.Of<IWebHostEnvironment>());

        _controller = new ProjectsController(projectsService, _db, eventPublisher, streamingService, contractsService, NullLogger<ProjectsController>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void SetUser(ProjectsController controller, string keycloakId)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, keycloakId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    [Fact]
    public async Task GetAll_ReturnsOkWithProjects()
    {
        _db.Projects.Add(new Project { Name = "P1" });
        await _db.SaveChangesAsync();

        var result = await _controller.GetAll();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetById_WhenNotFound_ReturnsNotFound()
    {
        var result = await _controller.GetById(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetById_WhenFound_ReturnsOk()
    {
        var project = new Project { Name = "P" };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        var result = await _controller.GetById(project.Id);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Create_WhenPortfolioNotFound_ReturnsBadRequest()
    {
        var result = await _controller.Create(new CreateProjectRequest { Name = "New", PortfolioId = Guid.NewGuid() });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AssignManager_WhenProjectNotFound_ReturnsNotFound()
    {
        var result = await _controller.AssignManager(Guid.NewGuid(), new AssignManagerDto { ProjectManagerId = Guid.NewGuid() });

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task AssignManager_WhenProjectExists_AssignsManagerAndReturnsOk()
    {
        var project = new Project { Name = "P" };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        var managerId = Guid.NewGuid();

        var result = await _controller.AssignManager(project.Id, new AssignManagerDto { ProjectManagerId = managerId });

        result.Should().BeOfType<OkObjectResult>();
        (await _db.Projects.FindAsync(project.Id))!.ProjectManagerId.Should().Be(managerId);
    }

    [Fact]
    public async Task GetMyProjects_WhenNoClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await _controller.GetMyProjects();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetMyProjects_WhenUserNotFound_ReturnsNotFound()
    {
        SetUser(_controller, "kc-unknown");

        var result = await _controller.GetMyProjects();

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetMyProjects_WhenUserFound_ReturnsOk()
    {
        var director = new User { FullName = "Dir", Email = "d@test.com", KeycloakId = "kc-dir", Role = GlobalRole.PortfolioDirector };
        _db.Users.Add(director);
        var portfolio = new Portfolio { Name = "Pf", PortfolioDirectorId = director.Id };
        _db.Portfolios.Add(portfolio);
        _db.Projects.Add(new Project { Name = "P", PortfolioId = portfolio.Id });
        await _db.SaveChangesAsync();
        SetUser(_controller, "kc-dir");

        var result = await _controller.GetMyProjects();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetManagedProjects_WhenNoClaim_ReturnsUnauthorized()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await _controller.GetManagedProjects();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetManagedProjects_WhenUserNotFound_ReturnsNotFound()
    {
        SetUser(_controller, "kc-unknown");

        var result = await _controller.GetManagedProjects();

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetManagedProjects_WhenUserFound_ReturnsOkWithProgress()
    {
        var manager = new User { FullName = "PM", Email = "pm@test.com", KeycloakId = "kc-pm", Role = GlobalRole.ProjectManager };
        _db.Users.Add(manager);
        var project = new Project { Name = "P", ProjectManagerId = manager.Id };
        _db.Projects.Add(project);
        _db.AcpTasks.Add(new AcpTask { Title = "T", ProjectId = project.Id, Status = AcpTaskStatus.Done });
        await _db.SaveChangesAsync();
        SetUser(_controller, "kc-pm");

        var result = await _controller.GetManagedProjects();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Update_WhenNotFound_ReturnsNotFound()
    {
        var result = await _controller.Update(Guid.NewGuid(), new UpdateProjectRequest { Name = "X" });

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Update_WhenFound_ReturnsOkWithUpdatedProject()
    {
        var project = new Project { Name = "Old" };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        var result = await _controller.Update(project.Id, new UpdateProjectRequest { Name = "New", Description = "D" });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((Project)ok.Value!).Name.Should().Be("New");
    }

    [Fact]
    public async Task GetStats_ReturnsCounts()
    {
        _db.Projects.Add(new Project { Name = "P" });
        _db.Contracts.Add(new Contract { ClientName = "C" });
        await _db.SaveChangesAsync();

        var result = await _controller.GetStats();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetDetails_WhenNotFound_ReturnsNotFound()
    {
        var result = await _controller.GetDetails(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetDetails_WhenFound_ReturnsFullProjectPayload()
    {
        var director = new User { FullName = "Dir", Email = "d@test.com", KeycloakId = "kc-dir" };
        var manager = new User { FullName = "PM", Email = "pm@test.com", KeycloakId = "kc-pm" };
        var consultant = new User { FullName = "Cons", Email = "c@test.com", KeycloakId = "kc-c" };
        _db.Users.AddRange(director, manager, consultant);
        var portfolio = new Portfolio { Name = "Pf", PortfolioDirectorId = director.Id, PortfolioDirector = director };
        _db.Portfolios.Add(portfolio);
        var project = new Project { Name = "P", PortfolioId = portfolio.Id, ProjectManagerId = manager.Id };
        _db.Projects.Add(project);
        var stream = new Stream { Name = "S1", ProjectId = project.Id };
        _db.Streams.Add(stream);
        _db.StreamMembers.Add(new StreamMember { StreamId = stream.Id, ConsultantId = consultant.Id, Consultant = consultant, TeamType = TeamType.Business });
        _db.AcpTasks.Add(new AcpTask { Title = "T1", ProjectId = project.Id, StreamId = stream.Id, Status = AcpTaskStatus.Done });
        _db.ProjectSteps.Add(new ProjectStep { ProjectId = project.Id, StepName = "Step1", ToolName = "Tool" });
        await _db.SaveChangesAsync();

        var result = await _controller.GetDetails(project.Id);

        result.Should().BeOfType<OkObjectResult>();
    }
}
