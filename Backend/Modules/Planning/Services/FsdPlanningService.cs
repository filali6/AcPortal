using Backend.Data;
using Backend.Modules.Planning.Models;
using Backend.Modules.Planning.Tools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Backend.Modules.Auth.Models;

namespace Backend.Modules.Planning.Services;

public class FsdPlanningService
{
    private readonly Kernel _kernel;
    private readonly IConfiguration _config;
    
    private readonly ILogger<FsdPlanningService> _logger;
    private readonly AppDbContext _db;

    public FsdPlanningService(
        Kernel kernel,
        AppDbContext db,
        PlanningTools tools,
        IConfiguration config,
         
        ILogger<FsdPlanningService> logger)
    {
        _kernel = kernel;
        _config = config;
        _logger = logger;
        _db=db;

        // Enregistre les tools dans le kernel
        _kernel.Plugins.AddFromObject(tools, "PlanningTools");
    }

    public async Task<string?> GenerateAsync(string fsdText, string guidelines)
    {
        var systemPrompt = _config["Planning:SystemPrompt"]
            ?? "You are an expert ACP deployment consultant.";

        var prompt = $@"{systemPrompt}

INSTRUCTIONS:
1. Call get_planning_context() to get real plugins, leads and consultants
2. Read the FSD carefully and identify ACP modules to configure
3. Build the plan using ONLY IDs from the context
4. Call validate_proposal(json) to validate your plan
5. If validation fails → fix errors → revalidate
6. Return ONLY the final valid JSON — no explanation, no markdown

PM GUIDELINES: {guidelines}

MANDATORY RULES — follow exactly:
1. Each stream MUST include at least one business consultant in ""businessConsultantIds"" AND at least one technical consultant in ""technicalConsultantIds"", if such consultants exist. Never leave one of these arrays empty if consultants of that type are available.
2. Each stream MUST include a mix of Business steps and Technical steps — never generate a stream with only one type, unless the FSD content genuinely has no technical or no business work for that stream.
3. Every step MUST have a ""teamType"" field set to exactly ""Business"" or ""Technical"", matching the plugin's team type from the context. Never omit ""teamType"".
4. ALWAYS assign businessLeadId and technicalLeadId — never null if leads are available.
5. Use ONLY plugin IDs and user IDs returned by get_planning_context().

FSD DOCUMENT:
{fsdText}

Required JSON structure:
{{
  ""streams"": [
    {{
      ""name"": ""..."",
      ""description"": ""..."",
      ""rationale"": ""..."",
      ""businessLeadId"": ""guid"",
      ""technicalLeadId"": ""guid"",
      ""businessConsultantIds"": [""guid""],
      ""technicalConsultantIds"": [""guid""],
      ""steps"": [
        {{""stepName"": ""..."", ""pluginId"": ""..."", ""order"": 1, ""teamType"": ""Business""}}
      ]
    }}
  ]
}}";

        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        return await InvokeAsync(prompt, settings);
    }

    public async Task<string?> RefineAsync(
        string currentPlan,
        string conversation,
        string userMessage,
        string pluginsList,
        string peopleContext)
    {
        var prompt = $@"You are an expert ACP deployment consultant.

CURRENT PLAN (work on this exact plan):
{currentPlan}

CONVERSATION HISTORY:
{conversation}

AVAILABLE PLUGINS (use ONLY these IDs):
{pluginsList}

{peopleContext}

PM REQUEST: {userMessage}

Instructions:
- Apply exactly what the PM asked, nothing more
- If PM mentions a person by name, find their ID in the people list
- Keep everything else in the plan unchanged
- Respond ONLY with the complete updated JSON. No explanation. No markdown.";

        return await InvokeAsync(prompt);
    }

    private async Task<string?> InvokeAsync(string prompt, PromptExecutionSettings? settings = null)
    {
        var maxRetries = _config.GetValue("Planning:MaxRetries", 3);
        var delayMs = _config.GetValue("Planning:RetryDelayMs", 2000);

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var result = settings != null
                    ? await _kernel.InvokePromptAsync(prompt, new KernelArguments(settings))
                    : await _kernel.InvokePromptAsync(prompt);

                var json = CleanJson(result.ToString());

                if (string.IsNullOrWhiteSpace(json))
                {
                    if (i < maxRetries - 1)
                        await Task.Delay(delayMs * (int)Math.Pow(2, i));
                    continue;
                }

                return json;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Attempt {i}/{max}: {msg}", i + 1, maxRetries, ex.Message);
                if (i < maxRetries - 1)
                    await Task.Delay(delayMs * (int)Math.Pow(2, i));
                else
                    _logger.LogError(ex, "All attempts failed");
            }
        }
        return null;
    }
    private static string CleanJson(string raw)
    {
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("```")) return trimmed;
        var lines = trimmed.Split('\n');
        return string.Join('\n',
            lines.Skip(1).TakeWhile(l => !l.TrimStart().StartsWith("```")));
    }
}