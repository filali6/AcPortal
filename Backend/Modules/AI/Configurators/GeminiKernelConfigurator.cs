using Microsoft.SemanticKernel;

namespace Backend.Modules.AI.Configurators;

public class GeminiKernelConfigurator : IKernelConfigurator
{
    public string Provider => "gemini";

    public void Configure(IKernelBuilder builder, IConfiguration configuration)
    {
        var modelId = configuration["AI:ModelId"]!;
        var apiKey = configuration["AI:ApiKey"]!;
        builder.AddGoogleAIGeminiChatCompletion(modelId, apiKey);
    }
}