using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Backend.Data;
using Backend.Modules.AI.Services;
using Backend.Modules.Auth.Models;
using Backend.Modules.Contracts.Services;
using Backend.Modules.Events.Services;
using Backend.Modules.Git.Services;
using Backend.Modules.Planning.Controllers;
using Backend.Modules.Planning.Models;
using Backend.Modules.Planning.Services;
using Backend.Modules.Planning.Tools;
using Backend.Modules.Projects.Models;
using Backend.Modules.Tools.Models;
using Backend.Modules.Tools.Services;
using Dapr.Client;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Stream = Backend.Modules.Projects.Models.Stream;

namespace Backend.Tests.Planning;

public class PlanningControllerTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IConfiguration CreateConfig(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Planning:FsdUploadFolder"] = "uploads/fsd-tests",
            ["EventPublisher:MaxRetries"] = "1",
            ["EventPublisher:RetryDelayMs"] = "1"
        };
        if (overrides != null)
            foreach (var kv in overrides) values[kv.Key] = kv.Value;

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    // Builds a real FsdPlanningService backed by a Kernel whose chat completion is mocked,
    // since FsdPlanningService's public methods aren't virtual and can't be Moq'd directly.
    private static FsdPlanningService CreateFakePlanningService(AppDbContext db, string? assistantReply, bool alwaysThrow = false, int maxRetries = 1)
    {
        var chatMock = new Mock<IChatCompletionService>();
        chatMock.Setup(c => c.Attributes).Returns(new Dictionary<string, object?>());
        if (alwaysThrow)
        {
            chatMock.Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));
        }
        else
        {
            chatMock.Setup(c => c.GetChatMessageContentsAsync(It.IsAny<ChatHistory>(), It.IsAny<PromptExecutionSettings>(), It.IsAny<Kernel>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<ChatMessageContent> { new(AuthorRole.Assistant, assistantReply ?? string.Empty) });
        }

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(chatMock.Object);
        var kernel = builder.Build();

        var tools = new PlanningTools(db, new PluginRegistry(db));
        var config = CreateConfig(new Dictionary<string, string?>
        {
            ["Planning:MaxRetries"] = maxRetries.ToString(),
            ["Planning:RetryDelayMs"] = "0"
        });
        var invocationHelper = new KernelInvocationHelper(config, NullLogger<KernelInvocationHelper>.Instance);
        return new FsdPlanningService(kernel, db, tools, config, invocationHelper, NullLogger<FsdPlanningService>.Instance);
    }

    private static (PlanningController Controller, Mock<IPdfTextExtractor> Extractor, Mock<IGitProvider> GitProvider) BuildController(
        AppDbContext db,
        FsdPlanningService planningService,
        IConfiguration? config = null)
    {
        var extractorMock = new Mock<IPdfTextExtractor>();
        var gitProviderMock = new Mock<IGitProvider>();
        gitProviderMock
            .Setup(g => g.CreateRepoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>()))
            .ReturnsAsync("http://repo-url");

        var effectiveConfig = config ?? CreateConfig();
        var gitService = new GitService(gitProviderMock.Object, db, NullLogger<GitService>.Instance);
        var publisher = new EventPublisher(new DaprClientBuilder().Build(), NullLogger<EventPublisher>.Instance, effectiveConfig);

        var envMock = new Mock<IWebHostEnvironment>();
        envMock.Setup(e => e.ContentRootPath).Returns(Path.GetTempPath());

        var plugins = new PluginRegistry(db);

        var controller = new PlanningController(
            db, planningService, extractorMock.Object, publisher,
            gitService, envMock.Object, effectiveConfig,
            NullLogger<PlanningController>.Instance, plugins);

        return (controller, extractorMock, gitProviderMock);
    }

    private static IFormFile CreateFormFile(string content = "dummy fsd content", string fileName = "test.pdf")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
    }

    // ---------- Generate ----------

    [Fact]
    public async Task Generate_WhenFileIsNull_ShouldReturnBadRequest()
    {
        await using var db = CreateDb();
        var (controller, _, _) = BuildController(db, CreateFakePlanningService(db, "{\"streams\":[]}"));

        var result = await controller.Generate(null!, Guid.NewGuid(), null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Generate_WhenExtractionFails_ShouldReturnBadRequest()
    {
        await using var db = CreateDb();
        var (controller, extractor, _) = BuildController(db, CreateFakePlanningService(db, "{\"streams\":[]}"));
        extractor.Setup(e => e.Extract(It.IsAny<string>())).Throws(new Exception("cannot read pdf"));

        var result = await controller.Generate(CreateFormFile(), Guid.NewGuid(), null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Generate_WhenPlanningServiceFails_ShouldReturn500()
    {
        await using var db = CreateDb();
        var (controller, extractor, _) = BuildController(db, CreateFakePlanningService(db, assistantReply: null, alwaysThrow: true));
        extractor.Setup(e => e.Extract(It.IsAny<string>())).Returns("Extracted FSD text");

        var result = await controller.Generate(CreateFormFile(), Guid.NewGuid(), null);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task Generate_WithValidInputs_ShouldPersistProposalAndReturnPlan()
    {
        await using var db = CreateDb();
        var (controller, extractor, _) = BuildController(db, CreateFakePlanningService(db, "{\"streams\":[]}"));
        extractor.Setup(e => e.Extract(It.IsAny<string>())).Returns("Extracted FSD text");
        var projectId = Guid.NewGuid();

        var result = await controller.Generate(CreateFormFile(), projectId, "custom guidelines");

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var proposal = await db.PlanningProposals.SingleAsync();
        proposal.ProjectId.Should().Be(projectId);
        proposal.Guidelines.Should().Be("custom guidelines");
        proposal.ProposalJson.Should().Be("{\"streams\":[]}");
    }

    // ---------- Refine ----------

    [Fact]
    public async Task Refine_WhenProposalNotFound_ShouldReturnNotFound()
    {
        await using var db = CreateDb();
        var (controller, _, _) = BuildController(db, CreateFakePlanningService(db, "{\"streams\":[]}"));

        var result = await controller.Refine(Guid.NewGuid(), new PlanningController.RefineRequest { Message = "hi" });

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Refine_WhenProposalAlreadyApproved_ShouldReturnBadRequest()
    {
        await using var db = CreateDb();
        var proposal = new PlanningProposal { ProjectId = Guid.NewGuid(), ProposalJson = "{}", Status = "Approved" };
        db.PlanningProposals.Add(proposal);
        await db.SaveChangesAsync();
        var (controller, _, _) = BuildController(db, CreateFakePlanningService(db, "{\"streams\":[]}"));

        var result = await controller.Refine(proposal.Id, new PlanningController.RefineRequest { Message = "hi" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Refine_WithValidRequest_ShouldUpdateProposal()
    {
        await using var db = CreateDb();
        var proposal = new PlanningProposal { ProjectId = Guid.NewGuid(), ProposalJson = "{\"streams\":[]}", Status = "Draft" };
        db.PlanningProposals.Add(proposal);
        await db.SaveChangesAsync();
        var (controller, _, _) = BuildController(db, CreateFakePlanningService(db, "{\"streams\":[{\"name\":\"Updated\"}]}"));

        var result = await controller.Refine(proposal.Id, new PlanningController.RefineRequest { Message = "add a stream" });

        result.Should().BeOfType<OkObjectResult>();
        var updated = await db.PlanningProposals.FindAsync(proposal.Id);
        updated!.ProposalJson.Should().Be("{\"streams\":[{\"name\":\"Updated\"}]}");
    }

    // ---------- Approve ----------

    [Fact]
    public async Task Approve_WhenProposalNotFound_ShouldReturnNotFound()
    {
        await using var db = CreateDb();
        var (controller, _, _) = BuildController(db, CreateFakePlanningService(db, "{\"streams\":[]}"));

        var result = await controller.Approve(Guid.NewGuid(), null);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Approve_WhenAlreadyApproved_ShouldReturnBadRequest()
    {
        await using var db = CreateDb();
        var proposal = new PlanningProposal { ProjectId = Guid.NewGuid(), ProposalJson = "{\"streams\":[]}", Status = "Approved" };
        db.PlanningProposals.Add(proposal);
        await db.SaveChangesAsync();
        var (controller, _, _) = BuildController(db, CreateFakePlanningService(db, "{\"streams\":[]}"));

        var result = await controller.Approve(proposal.Id, null);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Approve_WhenProjectNotFound_ShouldReturnNotFound()
    {
        await using var db = CreateDb();
        var proposal = new PlanningProposal { ProjectId = Guid.NewGuid(), ProposalJson = "{\"streams\":[]}", Status = "Draft" };
        db.PlanningProposals.Add(proposal);
        await db.SaveChangesAsync();
        var (controller, _, _) = BuildController(db, CreateFakePlanningService(db, "{\"streams\":[]}"));

        var result = await controller.Approve(proposal.Id, null);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Approve_WithValidPlan_ShouldCreateStreamsMembersAndSteps()
    {
        await using var db = CreateDb();
        var project = new Project { Name = "Project 1" };
        db.Projects.Add(project);

        var bizLead = new User { FullName = "Biz Lead", Role = GlobalRole.BusinessTeamLead };
        var techLead = new User { FullName = "Tech Lead", Role = GlobalRole.TechnicalTeamLead };
        var bizConsultant = new User { FullName = "Biz Cons", Role = GlobalRole.Consultant, ConsultantType = ConsultantType.Business };
        var techConsultant = new User { FullName = "Tech Cons", Role = GlobalRole.Consultant, ConsultantType = ConsultantType.Technical };
        db.Users.AddRange(bizLead, techLead, bizConsultant, techConsultant);

        var planJson = JsonSerializer.Serialize(new
        {
            streams = new[]
            {
                new
                {
                    name = "Stream 1",
                    businessLeadId = bizLead.Id.ToString(),
                    technicalLeadId = techLead.Id.ToString(),
                    businessConsultantIds = new[] { bizConsultant.Id.ToString() },
                    technicalConsultantIds = new[] { techConsultant.Id.ToString() },
                    steps = new[]
                    {
                        new { stepName = "Step 1", pluginId = "plugin-1", order = 1, teamType = "Business" },
                        new { stepName = "Step 2", pluginId = "plugin-2", order = 2, teamType = "Technical" }
                    }
                }
            }
        });

        var proposal = new PlanningProposal { ProjectId = project.Id, ProposalJson = planJson, Status = "Draft" };
        db.PlanningProposals.Add(proposal);
        await db.SaveChangesAsync();

        var (controller, _, gitProvider) = BuildController(db, CreateFakePlanningService(db, "{\"streams\":[]}"));

        var result = await controller.Approve(proposal.Id, null);

        result.Should().BeOfType<OkObjectResult>();

        var savedStream = await db.Streams.SingleAsync();
        savedStream.Name.Should().Be("Stream 1");
        savedStream.BusinessTeamLeadId.Should().Be(bizLead.Id);
        savedStream.TechnicalTeamLeadId.Should().Be(techLead.Id);

        var members = await db.StreamMembers.Where(m => m.StreamId == savedStream.Id).ToListAsync();
        members.Should().HaveCount(2);
        members.Should().Contain(m => m.ConsultantId == bizConsultant.Id && m.TeamType == TeamType.Business);
        members.Should().Contain(m => m.ConsultantId == techConsultant.Id && m.TeamType == TeamType.Technical);

        var steps = await db.ProjectSteps.Where(s => s.StreamId == savedStream.Id).ToListAsync();
        steps.Should().HaveCount(2);
        steps.Should().Contain(s => s.StepName == "Step 1" && s.TeamType == TeamType.Business);
        steps.Should().Contain(s => s.StepName == "Step 2" && s.TeamType == TeamType.Technical);

        var updatedProposal = await db.PlanningProposals.FindAsync(proposal.Id);
        updatedProposal!.Status.Should().Be("Approved");

        gitProvider.Verify(g => g.CreateRepoAsync(savedStream.Id, "Stream 1", project.Id), Times.Once);
    }

    // ---------- Reject ----------

    [Fact]
    public async Task Reject_WhenProposalNotFound_ShouldReturnNotFound()
    {
        await using var db = CreateDb();
        var (controller, _, _) = BuildController(db, CreateFakePlanningService(db, "{\"streams\":[]}"));

        var result = await controller.Reject(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Reject_ShouldMarkProposalAsRejected()
    {
        await using var db = CreateDb();
        var proposal = new PlanningProposal { ProjectId = Guid.NewGuid(), ProposalJson = "{}", Status = "Draft" };
        db.PlanningProposals.Add(proposal);
        await db.SaveChangesAsync();
        var (controller, _, _) = BuildController(db, CreateFakePlanningService(db, "{\"streams\":[]}"));

        var result = await controller.Reject(proposal.Id);

        result.Should().BeOfType<OkObjectResult>();
        var updated = await db.PlanningProposals.FindAsync(proposal.Id);
        updated!.Status.Should().Be("Rejected");
    }

    // ---------- GetByProject ----------

    [Fact]
    public async Task GetByProject_ShouldReturnProposalsOrderedByCreatedAtDescending()
    {
        await using var db = CreateDb();
        var projectId = Guid.NewGuid();
        var older = new PlanningProposal { ProjectId = projectId, ProposalJson = "{}", CreatedAt = DateTime.UtcNow.AddDays(-1) };
        var newer = new PlanningProposal { ProjectId = projectId, ProposalJson = "{}", CreatedAt = DateTime.UtcNow };
        var otherProject = new PlanningProposal { ProjectId = Guid.NewGuid(), ProposalJson = "{}" };
        db.PlanningProposals.AddRange(older, newer, otherProject);
        await db.SaveChangesAsync();
        var (controller, _, _) = BuildController(db, CreateFakePlanningService(db, "{\"streams\":[]}"));

        var result = await controller.GetByProject(projectId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var items = ((IEnumerable<object>)ok.Value!).ToList();
        items.Should().HaveCount(2);
    }

    // ---------- GetStreamSteps ----------

    [Fact]
    public async Task GetStreamSteps_ShouldReturnStepsOrderedByOrder()
    {
        await using var db = CreateDb();
        var project = new Project { Name = "P" };
        db.Projects.Add(project);
        var stream = new Stream { Name = "S", ProjectId = project.Id };
        db.Streams.Add(stream);
        db.ProjectSteps.AddRange(
            new ProjectStep { ProjectId = project.Id, StreamId = stream.Id, StepName = "Second", Order = 2 },
            new ProjectStep { ProjectId = project.Id, StreamId = stream.Id, StepName = "First", Order = 1 });
        await db.SaveChangesAsync();
        var (controller, _, _) = BuildController(db, CreateFakePlanningService(db, "{\"streams\":[]}"));

        var result = await controller.GetStreamSteps(stream.Id);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var items = ((IEnumerable<object>)ok.Value!).ToList();
        items.Should().HaveCount(2);
    }

    // ---------- ApproveStreamSteps ----------

    [Fact]
    public async Task ApproveStreamSteps_WhenStreamNotFound_ShouldReturnNotFound()
    {
        await using var db = CreateDb();
        var (controller, _, _) = BuildController(db, CreateFakePlanningService(db, "{\"streams\":[]}"));
        SetUser(controller, Guid.NewGuid());

        var result = await controller.ApproveStreamSteps(Guid.NewGuid(), new PlanningController.ApproveStepsRequest());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ApproveStreamSteps_WithModifiedSteps_ShouldReplaceExistingSteps()
    {
        await using var db = CreateDb();
        var project = new Project { Name = "P" };
        db.Projects.Add(project);
        var stream = new Stream { Name = "S", ProjectId = project.Id };
        db.Streams.Add(stream);
        db.ProjectSteps.Add(new ProjectStep { ProjectId = project.Id, StreamId = stream.Id, StepName = "Old", Order = 1 });
        var keycloakId = Guid.NewGuid().ToString();
        var lead = new User { FullName = "Lead", Role = GlobalRole.BusinessTeamLead, KeycloakId = keycloakId };
        db.Users.Add(lead);
        await db.SaveChangesAsync();

        var (controller, _, _) = BuildController(db, CreateFakePlanningService(db, "{\"streams\":[]}"));
        SetUser(controller, keycloakId);

        var request = new PlanningController.ApproveStepsRequest
        {
            Steps = new List<StepDto>
            {
                new() { StepName = "New Step", ToolName = "tool-1", Order = 1 }
            }
        };

        var result = await controller.ApproveStreamSteps(stream.Id, request);

        result.Should().BeOfType<OkObjectResult>();
        var steps = await db.ProjectSteps.Where(s => s.StreamId == stream.Id).ToListAsync();
        steps.Should().ContainSingle(s => s.StepName == "New Step");
    }

    private static void SetUser(PlanningController controller, Guid keycloakId) => SetUser(controller, keycloakId.ToString());

    private static void SetUser(PlanningController controller, string keycloakId)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, keycloakId) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }
}
