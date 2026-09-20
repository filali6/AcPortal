using Backend.Modules.Events.Controllers;
using Backend.Modules.Events.Handlers;
using Backend.Modules.Events.Models;
using Backend.Modules.Events.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Backend.Tests.Events;

[Collection("WorkflowConfigFile")]
public class WorkflowControllerTests : IDisposable
{
    private readonly string _configPath;

    public WorkflowControllerTests()
    {
        _configPath = Path.Combine(Directory.GetCurrentDirectory(), "workflow-config.json");
        File.WriteAllText(_configPath, """
        {
          "workflowRules": [
            { "eventCode": "ContratSigné", "actionType": "CREATE_TASK", "targetType": "ROLE", "targetValues": ["HeadOfCDS"] }
          ]
        }
        """);
    }

    public void Dispose()
    {
        // restore a valid config so other test classes relying on it aren't affected
        File.WriteAllText(_configPath, """
        {
          "workflowRules": [
            { "eventCode": "ContratSigné", "actionType": "CREATE_TASK", "targetType": "ROLE", "targetValues": ["HeadOfCDS"] },
            { "eventCode": "ContratSigné", "actionType": "SUMMARIZE_CONTRACT" },
            { "eventCode": "ProjetCréé", "actionType": "CREATE_TASK", "targetType": "CONTEXT_USER", "targetValues": ["DirectorId"] }
          ]
        }
        """);
    }

    private static WorkflowController CreateController(params string[] actionTypes)
    {
        var logger = NullLogger<WorkflowRulesService>.Instance;
        var rulesService = new WorkflowRulesService(logger);
        var handlers = actionTypes.Select(t =>
        {
            var mock = new Mock<IActionHandler>();
            mock.Setup(h => h.ActionType).Returns(t);
            return mock.Object;
        });
        return new WorkflowController(rulesService, handlers);
    }

    [Fact]
    public void GetAll_ReturnsAllRules()
    {
        var controller = CreateController();

        var result = controller.GetAll();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((List<WorkflowRule>)ok.Value!).Should().ContainSingle(r => r.EventCode == "ContratSigné");
    }

    [Fact]
    public void Update_WithValidConfig_WritesFileAndReturnsOk()
    {
        var controller = CreateController();
        var config = new WorkflowConfig
        {
            WorkflowRules = new List<WorkflowRule>
            {
                new() { EventCode = "NewEvent", ActionType = "CREATE_TASK", TargetType = "ROLE" }
            }
        };

        var result = controller.Update(config);

        result.Should().BeOfType<OkObjectResult>();
        File.ReadAllText(_configPath).Should().Contain("NewEvent");
    }

    [Fact]
    public void GetActionTypes_ReturnsDistinctSortedTypes()
    {
        var controller = CreateController("CREATE_TASK", "SUMMARIZE_CONTRACT", "CREATE_TASK");

        var result = controller.GetActionTypes();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((List<string>)ok.Value!).Should().Equal("CREATE_TASK", "SUMMARIZE_CONTRACT");
    }

    [Fact]
    public void GetTargetTypes_ReturnsFixedList()
    {
        var controller = CreateController();

        var result = controller.GetTargetTypes();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ((List<string>)ok.Value!).Should().Equal("ROLE", "CONTEXT_USER", "BEST_CONSULTANT");
    }
}
