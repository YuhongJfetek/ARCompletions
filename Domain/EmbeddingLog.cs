namespace ARCompletions.Domain;

public class EmbeddingLog
{
    public string Id { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public string EmbeddingJobId { get; set; } = default!;
    public string ActionType { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string? DetailJson { get; set; }
    public long CreatedAt { get; set; }
}
