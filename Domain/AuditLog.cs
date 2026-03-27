namespace ARCompletions.Domain;

public class AuditLog
{
    public string Id { get; set; } = default!;
    // 操作者：可記 actor type/id 或 email
    public string Actor { get; set; } = default!;
    // 動作名稱，例如 "EmbeddingJob.Retry", "PlatformUser.Create"
    public string Action { get; set; } = default!;
    // 相關目標 Id（若有）
    public string? TargetId { get; set; }
    // 儲存變更或額外資料的 JSON
    public string? Payload { get; set; }
    // Unix timestamp
    public long Timestamp { get; set; }
}
