namespace ARCompletions.Domain;

public class BotLlmLog
{
    public long LlmLogId { get; set; }
    public long? EventRowId { get; set; }
    public string? LogEvent { get; set; }
    public string? Task { get; set; }
    public string? FaqId { get; set; }
    public string? MatchedBy { get; set; }
    public double? Confidence { get; set; }
    public string? Model { get; set; }
    public string? ReplyMode { get; set; }
    public string? Reason { get; set; }
    public string? ErrorMessage { get; set; }
    public System.DateTimeOffset CreatedAt { get; set; } = System.DateTimeOffset.UtcNow;
}
