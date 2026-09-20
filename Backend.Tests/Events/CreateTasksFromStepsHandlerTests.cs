using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Events.Handlers;
using Backend.Modules.Events.Models;
using Backend.Modules.Projects.Models;
using Backend.Modules.Tasks.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ProjectStream = Backend.Modules.Projects.Models.Stream;

namespace Backend.Tests.Events;

public class CreateTasksFromStepsHandlerTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly CreateTasksFromStepsHandler _handler;

    public CreateTasksFromStepsHandlerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
        _handler = new CreateTasksFromStepsHandler(_db, NullLogger<CreateTasksFromStepsHandler>.Instance);
    }

   public void Dispose()
{
    _db.Dispose();
    GC.SuppressFinalize(this);
}

    private async Task<(Project project, ProjectStream stream)> SeedProjectAndStreamAsync()
    {
        var project = new Project { Name = "Project" };
        _db.Projects.Add(project);
        var stream = new ProjectStream { Name = "Stream", Project = project };
        _db.Streams.Add(stream);
        await _db.SaveChangesAsync();
        return (project, stream);
    }

    [Fact]
    public async Task HandleAsync_NoOp_WhenStreamIdMissing()
    {
        var (project, _) = await SeedProjectAndStreamAsync();

        await _handler.HandleAsync(new WorkflowRule(), new AcpEventDto { StreamId = null }, project.Id);

        (await _db.AcpTasks.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_NoOp_WhenProjectIdMissing()
    {
        var (_, stream) = await SeedProjectAndStreamAsync();

        await _handler.HandleAsync(new WorkflowRule(), new AcpEventDto { StreamId = stream.Id }, null);

        (await _db.AcpTasks.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_NoOp_WhenNoStepsFoundForStream()
    {
        var (project, stream) = await SeedProjectAndStreamAsync();

        await _handler.HandleAsync(new WorkflowRule(), new AcpEventDto { StreamId = stream.Id, LeadRole = "BusinessTeamLead" }, project.Id);

        (await _db.AcpTasks.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_AssignsStepToLeastBusyConsultant()
    {
        var (project, stream) = await SeedProjectAndStreamAsync();

        var busyConsultant = new User { FullName = "Busy", Email = "busy@test.com", KeycloakId = "kc-busy" };
        var freeConsultant = new User { FullName = "Free", Email = "free@test.com", KeycloakId = "kc-free" };
        _db.Users.AddRange(busyConsultant, freeConsultant);
        await _db.SaveChangesAsync();

        _db.StreamMembers.AddRange(
            new StreamMember { StreamId = stream.Id, ConsultantId = busyConsultant.Id, TeamType = TeamType.Business },
            new StreamMember { StreamId = stream.Id, ConsultantId = freeConsultant.Id, TeamType = TeamType.Business });

        // Give the "busy" consultant an existing active task so they're deprioritized.
        _db.AcpTasks.Add(new AcpTask { Title = "Existing", AssignedTo = "kc-busy", Status = AcpTaskStatus.Pending });

        _db.ProjectSteps.Add(new ProjectStep { ProjectId = project.Id, StreamId = stream.Id, StepName = "Step1", ToolName = "tool", TeamType = TeamType.Business });
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(new WorkflowRule(), new AcpEventDto { StreamId = stream.Id, LeadRole = "BusinessTeamLead" }, project.Id);

        var createdTask = await _db.AcpTasks.SingleAsync(t => t.Title == "Step1");
        createdTask.AssignedTo.Should().Be("kc-free");
        createdTask.ProjectId.Should().Be(project.Id);
        createdTask.StreamId.Should().Be(stream.Id);
    }

    [Fact]
    public async Task HandleAsync_SkipsStep_WhenNoConsultantFoundForTeamType()
    {
        var (project, stream) = await SeedProjectAndStreamAsync();
        _db.ProjectSteps.Add(new ProjectStep { ProjectId = project.Id, StreamId = stream.Id, StepName = "Orphan", ToolName = "tool", TeamType = TeamType.Technical });
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(new WorkflowRule(), new AcpEventDto { StreamId = stream.Id, LeadRole = "TechnicalTeamLead" }, project.Id);

        (await _db.AcpTasks.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_UsesStepTeamType_WhenSet_OverridingEventDtoLeadRole()
    {
        var (project, stream) = await SeedProjectAndStreamAsync();

        var technicalConsultant = new User { FullName = "Tech", Email = "tech@test.com", KeycloakId = "kc-tech" };
        _db.Users.Add(technicalConsultant);
        await _db.SaveChangesAsync();
        _db.StreamMembers.Add(new StreamMember { StreamId = stream.Id, ConsultantId = technicalConsultant.Id, TeamType = TeamType.Technical });
        // Step explicitly targets the Technical team even though the event says BusinessTeamLead.
        _db.ProjectSteps.Add(new ProjectStep { ProjectId = project.Id, StreamId = stream.Id, StepName = "TechStep", ToolName = "tool", TeamType = TeamType.Technical });
        await _db.SaveChangesAsync();

        await _handler.HandleAsync(new WorkflowRule(), new AcpEventDto { StreamId = stream.Id, LeadRole = "BusinessTeamLead" }, project.Id);

        var createdTask = await _db.AcpTasks.SingleAsync(t => t.Title == "TechStep");
        createdTask.AssignedTo.Should().Be("kc-tech");
    }
}
