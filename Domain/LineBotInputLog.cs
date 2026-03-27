namespace ARCompletions.Domain;

public class LineBotInputLog
{
    public string Id { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public string? ConversationId { get; set; }
    public string ExternalUserId { get; set; } = default!;
    public string? DisplayName { get; set; }
    public string MessageType { get; set; } = default!;
    public string? MessageText { get; set; }
    public string ExternalMessageId { get; set; } = default!;
    public string? ReplyToken { get; set; }
    public string RawJson { get; set; } = default!;
    public long ReceivedAt { get; set; }
    public long CreatedAt { get; set; }
}
