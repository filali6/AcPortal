using Microsoft.SemanticKernel;

namespace Backend.Modules.AI.Configurators;

public class GroqKernelConfigurator : IKernelConfigurator
{
    public string Provider => "groq";

    public void Configure(IKernelBuilder builder, IConfiguration configuration)
    {
        var modelId = configuration["AI:ModelId"]!;
        var apiKey = configuration["AI:ApiKey"]!;

        builder.AddOpenAIChatCompletion(
            modelId: modelId,
            apiKey: apiKey,
            httpClient: new HttpClient
            {
                BaseAddress = new Uri("https://api.groq.com/openai/v1/")
            }
        );
    }
}