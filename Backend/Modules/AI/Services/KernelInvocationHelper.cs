using Microsoft.SemanticKernel;

namespace Backend.Modules.AI.Services;

// Point d'entrée unique pour appeler le Kernel avec retry/backoff.
// Évite de dupliquer cette logique dans chaque service IA (FsdPlanningService,
// SlaAgentService, et tout futur service qui appellera le Kernel).
public class KernelInvocationHelper
{
    private readonly IConfiguration _config;
    private readonly ILogger<KernelInvocationHelper> _logger;

    public KernelInvocationHelper(IConfiguration config, ILogger<KernelInvocationHelper> logger)
    {
        _config = config;
        _logger = logger;
    }

    // configPrefix permet à chaque service de garder ses propres réglages
    // (ex: "Planning:MaxRetries" vs "Sla:MaxRetries") tout en partageant le code.
    public async Task<string?> InvokeAsync(
        Kernel kernel,
        string prompt,
        PromptExecutionSettings? settings = null,
        string configPrefix = "Planning")
    {
        var maxRetries = _config.GetValue($"{configPrefix}:MaxRetries", 3);
        var delayMs = _config.GetValue($"{configPrefix}:RetryDelayMs", 2000);

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                var result = settings != null
                    ? await kernel.InvokePromptAsync(prompt, new KernelArguments(settings))
                    : await kernel.InvokePromptAsync(prompt);

                var cleaned = CleanJson(result.ToString());

                if (!string.IsNullOrWhiteSpace(cleaned))
                    return cleaned;

                if (i < maxRetries - 1)
                    await Task.Delay(delayMs * (int)Math.Pow(2, i));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Kernel invocation attempt {I}/{Max} failed: {Msg}", i + 1, maxRetries, ex.Message);
                if (i < maxRetries - 1)
                    await Task.Delay(delayMs * (int)Math.Pow(2, i));
                else
                    _logger.LogError(ex, "All kernel invocation attempts failed");
            }
        }

        return null;
    }

    // Retire les ``` ```json éventuels autour de la réponse du LLM
    public static string CleanJson(string raw)
    {
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("```")) return trimmed;
        var lines = trimmed.Split('\n');
        return string.Join('\n', lines.Skip(1).TakeWhile(l => !l.TrimStart().StartsWith("```")));
    }
}