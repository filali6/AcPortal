using Backend.Data;
using Backend.Modules.AI.Services;
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
    private readonly KernelInvocationHelper _invocationHelper;
    private readonly ILogger<FsdPlanningService> _logger;
    private readonly AppDbContext _db;

    public FsdPlanningService(
        Kernel kernel,
        AppDbContext db,
        PlanningTools tools,
        IConfiguration config,
        KernelInvocationHelper invocationHelper,
        ILogger<FsdPlanningService> logger)
    {
        _kernel = kernel;
        _config = config;
        _invocationHelper = invocationHelper;
        _logger = logger;
        _db = db;

        // Enregistre les tools dans le kernel
        _kernel.Plugins.AddFromObject(tools, "PlanningTools");
    }

    // projectId optionnel : ne casse pas l'appelant actuel.
    // Tant qu'il n'est pas transmis, l'agent estime sans contrainte de calendrier.
    public async Task<string?> GenerateAsync(string fsdText, string guidelines, Guid? projectId = null)
    {
        var systemPrompt = _config["Planning:SystemPrompt"]
            ?? "You are an expert ACP deployment consultant.";

        var scheduleContext = await BuildScheduleContextAsync(projectId);

        var prompt = $@"{systemPrompt}

INSTRUCTIONS:
1. Call get_planning_context() to get real plugins, leads and consultants
2. Call get_standard_sla() to get the company's standard task SLA
3. Read the FSD carefully and identify ACP modules to configure
4. Build the plan using ONLY IDs from the context
5. Estimate a realistic duration for every step, based on the FSD content, the standard SLA and the project schedule below
6. Call validate_proposal(json) to validate your plan
7. If validation fails → fix errors → revalidate
8. Return ONLY the final valid JSON — no explanation, no markdown

PM GUIDELINES: {guidelines}

{scheduleContext}

MANDATORY RULES — follow exactly:
1. Each stream MUST include at least one business consultant in ""businessConsultantIds"" AND at least one technical consultant in ""technicalConsultantIds"", if such consultants exist. Never leave one of these arrays empty if consultants of that type are available.
2. Each stream MUST include a mix of Business steps and Technical steps — never generate a stream with only one type, unless the FSD content genuinely has no technical or no business work for that stream.
3. Every step MUST have a ""teamType"" field set to exactly ""Business"" or ""Technical"", matching the plugin's team type from the context. Never omit ""teamType"".
4. ALWAYS assign businessLeadId and technicalLeadId — never null if leads are available.
5. Use ONLY plugin IDs and user IDs returned by get_planning_context().
6. Every step MUST have an ""estimatedDays"" field: a positive integer (calendar days) reflecting the real complexity of that step as described in the FSD. A trivial parameter change and a complex multi-rule configuration must NOT receive the same estimate. Never omit ""estimatedDays"".
7. All steps are independent and run in parallel (no sequencing between steps). Each step's ""estimatedDays"" must fit within the available calendar days (if a schedule is given). If a realistic estimate cannot fit, keep the realistic estimate and explain the overrun in that step's ""durationRationale"" — never shrink estimates artificially.
8. SLA rules (from get_standard_sla) are a reference, not a hard limit: your added value is to be MORE precise per step. For each step, find the applicable Task rule (same domain as the step's plugin, otherwise the default one). If the estimate is more than double or less than half of that rule's ""slaDays"", add a short ""durationRationale"" to that step explaining why, based on the FSD content.
9. A stream lasts as long as its longest step. If that exceeds the default Stream rule's ""slaDays"", explain why in the stream's ""rationale"".
10. WORKLOAD: ""activeStreams"" in get_planning_context is each person's current number of ACTIVE streams (lists are sorted from least to most loaded). For each role, prefer the people with the fewest active streams. Also spread the streams of THIS plan across people — do not give every new stream to the same lead. If you choose someone more loaded than another available person of the same role, justify it in the stream's ""rationale"" (e.g. specific expertise required by the FSD).
11. JSON SAFETY: all text fields (name, description, rationale, durationRationale, stepName) are plain text. NEVER put double quotes or backslashes inside them — use single quotes if you need to quote something.

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
        {{""stepName"": ""..."", ""pluginId"": ""..."", ""order"": 1, ""teamType"": ""Business"", ""estimatedDays"": 5, ""durationRationale"": ""optional""}}
      ]
    }}
  ]
}}";

        var settings = new PromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var raw = await _invocationHelper.InvokeAsync(_kernel, prompt, settings, configPrefix: "Planning");
        return await EnsureValidPlanJsonAsync(raw);
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
- Every step MUST keep its ""teamType"" and ""estimatedDays"" fields (positive integer, calendar days)
- If the PM changes a duration, update ""estimatedDays"" accordingly; if you add a new step, give it a realistic ""estimatedDays""
- If you must choose a new person yourself, prefer the one with the fewest active streams (shown in the people list)
- Text fields are plain text: NEVER put double quotes or backslashes inside them
- Respond ONLY with the complete updated JSON. No explanation. No markdown.";

        var raw = await _invocationHelper.InvokeAsync(_kernel, prompt, configPrefix: "Planning");
        return await EnsureValidPlanJsonAsync(raw);
    }

    // ───────────────────────── Sécurité JSON ─────────────────────────

    // Un LLM ne renvoie pas toujours un JSON valide (balises ```json, texte autour,
    // guillemets mal échappés...). On nettoie, et si besoin on demande UNE réparation.
    // Renvoie null si le JSON reste invalide → le controller répond une erreur propre.
    private async Task<string?> EnsureValidPlanJsonAsync(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var candidate = ExtractJsonObject(raw);
        if (IsValidPlanJson(candidate)) return candidate;

        _logger.LogWarning("Planning: invalid JSON from the AI, asking for a repair. Start of response: {Raw}",
            raw.Length > 500 ? raw[..500] : raw);

        var repairPrompt = $@"The text below should be a JSON object but it is INVALID JSON.
Return ONLY the corrected valid JSON object, with exactly the same content and structure.
Only fix the syntax (quotes, escaping, commas, brackets). Remove any double quote or backslash inside text values.
No explanation, no markdown.

{candidate}";

        var repaired = await _invocationHelper.InvokeAsync(_kernel, repairPrompt, configPrefix: "Planning");
        if (string.IsNullOrWhiteSpace(repaired)) return null;

        var repairedCandidate = ExtractJsonObject(repaired);
        if (IsValidPlanJson(repairedCandidate)) return repairedCandidate;

        _logger.LogError("Planning: JSON still invalid after repair attempt");
        return null;
    }

    // Retire les balises markdown et garde seulement ce qui va du premier '{' au dernier '}'
    private static string ExtractJsonObject(string text)
    {
        var t = text.Trim();
        if (t.StartsWith("```"))
        {
            var firstLineEnd = t.IndexOf('\n');
            t = firstLineEnd >= 0 ? t[(firstLineEnd + 1)..] : t;
            if (t.EndsWith("```")) t = t[..^3];
        }

        var start = t.IndexOf('{');
        var end = t.LastIndexOf('}');
        return start >= 0 && end > start ? t[start..(end + 1)] : t;
    }

    // Valide = JSON lisible + objet racine avec un tableau "streams"
    private static bool IsValidPlanJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("streams", out var streams)
                && streams.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // Construit le bloc "PROJECT SCHEDULE" injecté dans le prompt.
    private async Task<string> BuildScheduleContextAsync(Guid? projectId)
    {
        if (projectId == null)
            return "PROJECT SCHEDULE: unknown — estimate durations from the FSD content only.";

        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId.Value)
            .Select(p => new { p.StartDate, p.TargetDate })
            .FirstOrDefaultAsync();

        if (project?.StartDate == null)
        {
            _logger.LogWarning("Planning: project {ProjectId} has no StartDate", projectId);
            return "PROJECT SCHEDULE: no start date defined — estimate durations from the FSD content only.";
        }

        var start = project.StartDate.Value.Date;

        if (project.TargetDate == null)
            return $@"PROJECT SCHEDULE:
- Start date: {start:yyyy-MM-dd}
- Target date: not defined — estimate durations from the FSD content only.";

        var target = project.TargetDate.Value.Date;
        var availableDays = (target - start).Days;

        if (availableDays <= 0)
            return $@"PROJECT SCHEDULE:
- Start date: {start:yyyy-MM-dd}
- Target date: {target:yyyy-MM-dd}
- WARNING: the target date is not after the start date. Give realistic estimates and flag the problem in each stream's ""rationale"".";

        return $@"PROJECT SCHEDULE:
- Start date: {start:yyyy-MM-dd}
- Target date: {target:yyyy-MM-dd}
- Available calendar days: {availableDays}";
    }
}