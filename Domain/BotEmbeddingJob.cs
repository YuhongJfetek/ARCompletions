namespace ARCompletions.Domain;

public class BotEmbeddingJob
{
    public System.Guid JobId { get; set; }
    public string Provider { get; set; } = string.Empty; // legacy_hash64 / openai
    public string Model { get; set; } = string.Empty;    // 對應模型名稱
    public string Scope { get; set; } = string.Empty;    // all / single
    public string? TargetFaqId { get; set; }
    public string Status { get; set; } = "pending";     // pending / running / completed / failed
    public int TotalCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public string TriggeredBy { get; set; } = "system"; // admin / system
    public System.DateTimeOffset? StartedAt { get; set; }
    public System.DateTimeOffset? FinishedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
