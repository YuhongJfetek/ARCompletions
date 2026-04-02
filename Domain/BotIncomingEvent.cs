namespace ARCompletions.Domain;

public class BotIncomingEvent
{
    public long EventRowId { get; set; }
    public string RawEventJson { get; set; } = default!;
    public string? EventType { get; set; }
    public string? MessageType { get; set; }
    public string? SourceType { get; set; }
    public string? LineUserId { get; set; }
    public string? LineGroupId { get; set; }
    public string? LineRoomId { get; set; }
    public string? ConversationId { get; set; }
    public string? ReplyToken { get; set; }
    public System.DateTimeOffset ReceivedAt { get; set; }
}
