using Backend.Data;
using Backend.Modules.Contracts.Models;
using Backend.Modules.Contracts.Services;
using Backend.Modules.Events.Models;
using Backend.Modules.Notifications.Services;

namespace Backend.Modules.Events.Handlers;

public class SummarizeContractHandler : IActionHandler
{
    public string ActionType => "SUMMARIZE_CONTRACT";

    private readonly AppDbContext _db;
    private readonly IPdfTextExtractor _extractor;
    private readonly IContractSummaryService _summaryService;
    private readonly NotificationService _notificationService;
    private readonly ILogger<SummarizeContractHandler> _logger;
    private readonly IWebHostEnvironment _env;

    public SummarizeContractHandler(
        AppDbContext db,
        IPdfTextExtractor extractor,
        IContractSummaryService summaryService,
        NotificationService notificationService,
        ILogger<SummarizeContractHandler> logger,
        IWebHostEnvironment env)
    {
        _db = db;
        _extractor = extractor;
        _summaryService = summaryService;
        _notificationService = notificationService;
        _logger = logger;
        _env = env;
    }

    public async Task HandleAsync(
        WorkflowRule rule,
        AcpEventDto eventDto,
        Guid? projectId)
    {
        if (eventDto.ContractId is null)
        {
            _logger.LogWarning("SummarizeContractHandler: ContractId is null");
            return;
        }

        var contract = await _db.Contracts.FindAsync(eventDto.ContractId);
        if (contract is null)
        {
            _logger.LogWarning("SummarizeContractHandler: Contract {Id} not found", eventDto.ContractId);
            return;
        }

        // Mark as pending
        contract.SummaryStatus = SummaryStatus.Pending;
        await _db.SaveChangesAsync();

        try
        {
            // 1. Get prompt from workflow rule config
            var prompt = rule.Config?.GetProperty("prompt").GetString()
                ?? "You are a senior legal and financial analyst. Generate a clear fluent narrative summary of this contract for a Head of CDS.";

            // 2. Extract text from first PDF
            var pdfFileName = contract.FilesPaths.FirstOrDefault();
            if (pdfFileName is null)
            {
                _logger.LogWarning("SummarizeContractHandler: No PDF found for contract {Id}", contract.Id);
                contract.SummaryStatus = SummaryStatus.Failed;
                await _db.SaveChangesAsync();
                return;
            }

            var fullPath = Path.Combine(_env.ContentRootPath, "uploads", pdfFileName);
            var extractedText = _extractor.Extract(fullPath);

            // 3. Summarize
            var summary = await _summaryService.SummarizeAsync(extractedText, prompt);

            // 4. Save
            contract.Summary = summary;
            contract.ExtractedText = extractedText;
            contract.SummaryStatus = SummaryStatus.Completed;
            contract.SummarizedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // 5. Notify DAF
            var dafUser = await _db.Users.FindAsync(contract.DafUserId);
            if (dafUser is not null)
            {
                await _notificationService.SendAsync(
                    dafUser.KeycloakId,
                    $"Summary ready for contract : {contract.ClientName}",
                    null
                );
            }

            _logger.LogInformation("Contract {Id} summarized successfully", contract.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to summarize contract {Id}", contract.Id);
            contract.SummaryStatus = SummaryStatus.Failed;
            await _db.SaveChangesAsync();
        }
    }
}