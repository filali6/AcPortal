using Backend.Modules.Events.Models;
using Backend.Modules.Events.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Backend.Modules.Events.Controllers;

[ApiController]
[Route("api/workflow")]
[Authorize(Roles = "SuperAdmin")]
public class WorkflowController : ControllerBase
{
    private readonly WorkflowRulesService _workflowRulesService;
    private readonly string _configPath;

    public WorkflowController(WorkflowRulesService workflowRulesService)
    {
        _workflowRulesService = workflowRulesService;
        _configPath = Path.Combine(Directory.GetCurrentDirectory(), "workflow-config.json");
    }

    // Lire toutes les règles
    [HttpGet]
    public IActionResult GetAll()
        => Ok(_workflowRulesService.GetAllRules());

    // Modifier les règles
    [HttpPut]
    public IActionResult Update([FromBody] WorkflowConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config,
                new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(_configPath, json);
            _workflowRulesService.ReloadRules();
            return Ok(new { message = "Workflow updated" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }

    // Options disponibles pour le frontend
    [HttpGet("action-types")]
    public IActionResult GetActionTypes() => Ok(new List<string>
    {
        "CREATE_TASK",
        "CREATE_TASKS_FROM_STEPS"
    });

    [HttpGet("target-types")]
    public IActionResult GetTargetTypes() => Ok(new List<string>
    {
        "ROLE",
        "CONTEXT_USER",
        "BEST_CONSULTANT"
    });
}