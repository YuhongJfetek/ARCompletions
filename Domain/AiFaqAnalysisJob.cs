namespace ARCompletions.Domain;

public class AiFaqAnalysisJob
{
    public string Id { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public string JobNo { get; set; } = default!;
    public string Status { get; set; } = default!;
    public long DateFrom { get; set; }
    public long DateTo { get; set; }
    public int ConversationCount { get; set; }
    public int MessageCount { get; set; }
    public int CandidateCount { get; set; }
    public string? ModelName { get; set; }
    public string? PromptVersion { get; set; }
    public long? StartedAt { get; set; }
    public long? FinishedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TriggeredByType { get; set; }
    public string? TriggeredById { get; set; }
    public long CreatedAt { get; set; }
}
