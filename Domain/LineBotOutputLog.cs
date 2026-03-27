namespace ARCompletions.Domain;

public class LineBotOutputLog
{
    public string Id { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public string? ConversationId { get; set; }
    public string ExternalUserId { get; set; } = default!;
    public string MessageType { get; set; } = default!;
    public string? MessageText { get; set; }
    public string? SourceType { get; set; }
    public string? SourceFaqId { get; set; }
    public double? ConfidenceScore { get; set; }
    public string? ModelName { get; set; }
    public string? PromptVersion { get; set; }
    public string RawJson { get; set; } = default!;
    public long SentAt { get; set; }
    public long CreatedAt { get; set; }
}
