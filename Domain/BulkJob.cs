namespace ARCompletions.Domain;

public class BulkJob
{
    public string Id { get; set; } = default!;
    public string Initiator { get; set; } = default!;
    public string Action { get; set; } = default!; // e.g., EmbeddingJob.BulkRetry
    public string? FilterJson { get; set; }
    public string Status { get; set; } = "queued"; // queued, processing, done, failed
    public long TotalCount { get; set; }
    public long ProcessedCount { get; set; }
    public string? ErrorMessage { get; set; }
    public long CreatedAt { get; set; }
    public long? UpdatedAt { get; set; }
}
