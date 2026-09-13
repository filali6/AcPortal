using Microsoft.SemanticKernel;

namespace Backend.Modules.Contracts.Services;

public class ContractSummaryService : IContractSummaryService
{
    private readonly Kernel _kernel;

    public ContractSummaryService(Kernel kernel)
    {
        _kernel = kernel;
    }

    public async Task<string> SummarizeAsync(string extractedText, string prompt)
    {
        var fullPrompt = $"{prompt}\n\nCONTRACT:\n{extractedText}";
        var result = await _kernel.InvokePromptAsync(fullPrompt);
        return result.ToString();
    }
}