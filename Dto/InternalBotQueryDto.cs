namespace ARCompletions.Dto;

public class InternalBotQueryDto
{
    public string VendorId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public long? Timestamp { get; set; }
}
