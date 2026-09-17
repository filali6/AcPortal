namespace Backend.Modules.Contracts.Services;

public interface IContractSummaryService
{
    Task<string> SummarizeAsync(string extractedText, string prompt);
}