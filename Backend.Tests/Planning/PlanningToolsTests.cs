using System.Text.Json;
using Backend.Data;
using Backend.Modules.Auth.Models;
using Backend.Modules.Planning.Tools;
using Backend.Modules.Projects.Models;
using Backend.Modules.Tools.Models;
using Backend.Modules.Tools.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Stream = Backend.Modules.Projects.Models.Stream;

namespace Backend.Tests.Planning;

public class PlanningToolsTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetPlanningContextAsync_ShouldReturnOnlyEligiblePlugins_WithCorrectTeamType()
    {
        // Arrange
        await using var db = CreateDb();
        db.PluginDefinitions.AddRange(
            new PluginDefinition { Id = "workflow-tool", Name = "Workflow Tool", IsActive = true, Url = "http://x", FunctionalDomain = "Workflow" },
            new PluginDefinition { Id = "finance-tool", Name = "Finance Tool", IsActive = true, Url = "http://x", FunctionalDomain = "Finance" },
            new PluginDefinition { Id = "inactive-tool", Name = "Inactive Tool", IsActive = false, Url = "http://x", FunctionalDomain = "Finance" },
            new PluginDefinition { Id = "no-url-tool", Name = "No Url Tool", IsActive = true, Url = "", FunctionalDomain = "Finance" },
            new PluginDefinition { Id = "no-domain-tool", Name = "No Domain Tool", IsActive = true, Url = "http://x", FunctionalDomain = null });
        await db.SaveChangesAsync();

        var tools = new PlanningTools(db, new PluginRegistry(db));

        // Act
        var json = await tools.GetPlanningContextAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json);
        var plugins = result.GetProperty("plugins").EnumerateArray().ToList();

        // Assert
        plugins.Should().HaveCount(2);
        plugins.Should().Contain(p => p.GetProperty("id").GetString() == "workflow-tool"
            && p.GetProperty("teamType").GetString() == "Technical");
        plugins.Should().Contain(p => p.GetProperty("id").GetString() == "finance-tool"
            && p.GetProperty("teamType").GetString() == "Business");
    }

    [Fact]
    public async Task GetPlanningContextAsync_ShouldReturnLeadsAndConsultants_WithWorkload()
    {
        // Arrange
        await using var db = CreateDb();

        var bizLead = new User { FullName = "Alice Biz", Role = GlobalRole.BusinessTeamLead };
        var techLead = new User { FullName = "Bob Tech", Role = GlobalRole.TechnicalTeamLead };
        var bizConsultant = new User { FullName = "Carl Cons", Role = GlobalRole.Consultant, ConsultantType = ConsultantType.Business };
        var techConsultant = new User { FullName = "Dana Cons", Role = GlobalRole.Consultant, ConsultantType = ConsultantType.Technical };
        db.Users.AddRange(bizLead, techLead, bizConsultant, techConsultant);

        var project = new Project { Name = "P1" };
        db.Projects.Add(project);

        var stream = new Stream { Name = "S1", ProjectId = project.Id, BusinessTeamLeadId = bizLead.Id };
        db.Streams.Add(stream);
        db.StreamMembers.Add(new StreamMember { StreamId = stream.Id, ConsultantId = bizConsultant.Id, TeamType = TeamType.Business });
        await db.SaveChangesAsync();

        var tools = new PlanningTools(db, new PluginRegistry(db));

        // Act
        var json = await tools.GetPlanningContextAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert
        var businessLeads = result.GetProperty("businessLeads").EnumerateArray().ToList();
        businessLeads.Should().ContainSingle(l => l.GetProperty("id").GetString() == bizLead.Id.ToString()
            && l.GetProperty("activeStreams").GetInt32() == 1);

        var technicalLeads = result.GetProperty("technicalLeads").EnumerateArray().ToList();
        technicalLeads.Should().ContainSingle(l => l.GetProperty("id").GetString() == techLead.Id.ToString()
            && l.GetProperty("activeStreams").GetInt32() == 0);

        var businessConsultants = result.GetProperty("businessConsultants").EnumerateArray().ToList();
        businessConsultants.Should().ContainSingle(c => c.GetProperty("id").GetString() == bizConsultant.Id.ToString()
            && c.GetProperty("activeStreams").GetInt32() == 1);

        var technicalConsultants = result.GetProperty("technicalConsultants").EnumerateArray().ToList();
        technicalConsultants.Should().ContainSingle(c => c.GetProperty("id").GetString() == techConsultant.Id.ToString()
            && c.GetProperty("activeStreams").GetInt32() == 0);
    }

    [Fact]
    public async Task ValidateProposalAsync_WithFullyValidPlan_ShouldReturnValidTrue()
    {
        // Arrange
        await using var db = CreateDb();
        db.PluginDefinitions.Add(new PluginDefinition { Id = "plugin-1", IsActive = true, Url = "http://x", FunctionalDomain = "Finance" });
        var lead = new User { FullName = "Lead", Role = GlobalRole.BusinessTeamLead };
        var consultant = new User { FullName = "Cons", Role = GlobalRole.Consultant, ConsultantType = ConsultantType.Business };
        db.Users.AddRange(lead, consultant);
        await db.SaveChangesAsync();

        var tools = new PlanningTools(db, new PluginRegistry(db));

        var proposal = JsonSerializer.Serialize(new
        {
            streams = new[]
            {
                new
                {
                    name = "Stream 1",
                    businessLeadId = lead.Id.ToString(),
                    businessConsultantIds = new[] { consultant.Id.ToString() },
                    steps = new[] { new { stepName = "Step 1", pluginId = "plugin-1" } }
                }
            }
        });

        // Act
        var json = await tools.ValidateProposalAsync(proposal);
        var result = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert
        result.GetProperty("valid").GetBoolean().Should().BeTrue();
        result.GetProperty("streamCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ValidateProposalAsync_WithUnknownPluginId_ShouldReturnError()
    {
        // Arrange
        await using var db = CreateDb();
        var tools = new PlanningTools(db, new PluginRegistry(db));

        var proposal = JsonSerializer.Serialize(new
        {
            streams = new[]
            {
                new { name = "Stream 1", steps = new[] { new { stepName = "Step 1", pluginId = "unknown-plugin" } } }
            }
        });

        // Act
        var json = await tools.ValidateProposalAsync(proposal);
        var result = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert
        result.GetProperty("valid").GetBoolean().Should().BeFalse();
        var errors = result.GetProperty("errors").EnumerateArray().Select(e => e.GetString()).ToList();
        errors.Should().ContainSingle(e => e!.Contains("plugin") && e.Contains("not eligible"));
    }

    [Fact]
    public async Task ValidateProposalAsync_WithUnknownConsultantIds_ShouldReturnErrors()
    {
        // Arrange
        await using var db = CreateDb();
        var tools = new PlanningTools(db, new PluginRegistry(db));
        var unknownId = Guid.NewGuid().ToString();

        var proposal = JsonSerializer.Serialize(new
        {
            streams = new[]
            {
                new
                {
                    name = "Stream 1",
                    businessConsultantIds = new[] { unknownId },
                    technicalConsultantIds = new[] { unknownId }
                }
            }
        });

        // Act
        var json = await tools.ValidateProposalAsync(proposal);
        var result = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert
        result.GetProperty("valid").GetBoolean().Should().BeFalse();
        var errors = result.GetProperty("errors").EnumerateArray().Select(e => e.GetString()).ToList();
        errors.Should().Contain(e => e!.Contains("business consultant") && e.Contains("not found"));
        errors.Should().Contain(e => e!.Contains("technical consultant") && e.Contains("not found"));
    }

    [Fact]
    public async Task ValidateProposalAsync_WithUnknownLeadIds_ShouldReturnErrors()
    {
        // Arrange
        await using var db = CreateDb();
        var tools = new PlanningTools(db, new PluginRegistry(db));
        var unknownId = Guid.NewGuid().ToString();

        var proposal = JsonSerializer.Serialize(new
        {
            streams = new[]
            {
                new { name = "Stream 1", businessLeadId = unknownId, technicalLeadId = unknownId }
            }
        });

        // Act
        var json = await tools.ValidateProposalAsync(proposal);
        var result = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert
        result.GetProperty("valid").GetBoolean().Should().BeFalse();
        var errors = result.GetProperty("errors").EnumerateArray().Select(e => e.GetString()).ToList();
        errors.Should().Contain(e => e!.Contains("business lead not found"));
        errors.Should().Contain(e => e!.Contains("technical lead not found"));
    }

    [Fact]
    public async Task ValidateProposalAsync_WithMalformedJson_ShouldReturnInvalidJsonError()
    {
        // Arrange
        await using var db = CreateDb();
        var tools = new PlanningTools(db, new PluginRegistry(db));

        // Act
        var json = await tools.ValidateProposalAsync("{ not valid json");
        var result = JsonSerializer.Deserialize<JsonElement>(json);

        // Assert
        result.GetProperty("valid").GetBoolean().Should().BeFalse();
        var errors = result.GetProperty("errors").EnumerateArray().Select(e => e.GetString()).ToList();
        errors.Should().ContainSingle(e => e!.StartsWith("Invalid JSON"));
    }
}
