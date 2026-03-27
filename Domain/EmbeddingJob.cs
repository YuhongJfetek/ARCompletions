namespace ARCompletions.Domain;

public class EmbeddingJob
{
    public string Id { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public string JobNo { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string TriggerType { get; set; } = default!;
    public int TotalFaqCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
    public string? JsonFilePath { get; set; }
    public string VectorVersion { get; set; } = default!;
    public string ModelName { get; set; } = default!;
    public long? StartedAt { get; set; }
    public long? FinishedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TriggeredByType { get; set; }
    public string? TriggeredById { get; set; }
    public long CreatedAt { get; set; }
}
