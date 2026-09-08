using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Backend.Modules.Events.Services;

namespace Backend.Tests.Workflow;

public class WorkflowRulesServiceTests
{
    private readonly string _testConfigPath;

    public WorkflowRulesServiceTests()
    {
        // Crée un fichier workflow-config.json temporaire pour les tests
        _testConfigPath = Path.Combine(
            AppContext.BaseDirectory,
            "workflow-config.json");

        var json = """
        {
          "workflowRules": [
            {
              "eventCode": "ContratSigné",
              "actionType": "CREATE_TASK",
              "targetType": "ROLE",
              "targetValues": ["HeadOfCDS"]
            },
            {
              "eventCode": "ContratSigné",
              "actionType": "SUMMARIZE_CONTRACT"
            },
            {
              "eventCode": "ProjetCréé",
              "actionType": "CREATE_TASK",
              "targetType": "CONTEXT_USER",
              "targetValues": ["DirectorId"]
            }
          ]
        }
        """;

        File.WriteAllText(_testConfigPath, json);
    }

    [Fact]
    public void GetRules_WithMatchingEventCode_ShouldReturnCorrectRules()
    {
        // Arrange
        var logger = NullLogger<WorkflowRulesService>.Instance;
        var service = new WorkflowRulesService(logger);

        // Act
        var result = service.GetRules("ContratSigné");

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(r =>
            r.EventCode.ToLower().Should().Be("contratsigné"));
    }

    [Fact]
    public void GetRules_WithNoMatchingEventCode_ShouldReturnEmpty()
    {
        // Arrange
        var logger = NullLogger<WorkflowRulesService>.Instance;
        var service = new WorkflowRulesService(logger);

        // Act
        var result = service.GetRules("EventInexistant");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAllRules_ShouldReturnAllRules()
    {
        // Arrange
        var logger = NullLogger<WorkflowRulesService>.Instance;
        var service = new WorkflowRulesService(logger);

        // Act
        var result = service.GetAllRules();

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public void GetRules_IsCaseInsensitive()
    {
        // Arrange
        var logger = NullLogger<WorkflowRulesService>.Instance;
        var service = new WorkflowRulesService(logger);

        // Act — même eventCode en minuscules
        var result = service.GetRules("contratsigné");

        // Assert
        result.Should().HaveCount(2);
    }
}