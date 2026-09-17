namespace Backend.Modules.Contracts.Models;

public enum ContractStatus
{
    Signed = 0,
    ProjectCreated = 1,
    InProgress = 2
}
public enum SummaryStatus
{
    None = 0,
    Pending = 1,
    Completed = 2,
    Failed = 3
}

public class Contract
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ClientName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ContractStatus Status { get; set; } = ContractStatus.Signed;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid DafUserId { get; set; }
    public Guid? ProjectId { get; set; }

    public List<string> FilesPaths { get; set; } = new();

    public string? Summary { get; set; }
    public string? ExtractedText { get; set; }
    public DateTime? SummarizedAt { get; set; }
    public SummaryStatus SummaryStatus { get; set; } = SummaryStatus.None;
}