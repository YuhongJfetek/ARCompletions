namespace ARCompletions.Domain;

public class ConversationMessage
{
    public string Id { get; set; } = default!;
    public string ConversationId { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public string Direction { get; set; } = default!; // in / out
    public string MessageType { get; set; } = default!;
    public string? MessageText { get; set; }
    public string RawJson { get; set; } = default!;
    public string? ExternalMessageId { get; set; }
    public string? ReplyToken { get; set; }
    public string? SenderId { get; set; }
    public string? SenderName { get; set; }
    public string? SourceFaqId { get; set; }
    public string? SourceType { get; set; }
    public double? ConfidenceScore { get; set; }
    public string? ModelName { get; set; }
    public string? PromptVersion { get; set; }
    public long CreatedAt { get; set; }
}
