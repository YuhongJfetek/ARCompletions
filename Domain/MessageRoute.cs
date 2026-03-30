namespace ARCompletions.Domain;

public class MessageRoute
{
    public string Id { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public string? InputLogId { get; set; }
    public string? ConversationId { get; set; }
    public string Route { get; set; } = default!;
    public string? Reason { get; set; }
    public string? MatchedFaqId { get; set; }
    public double? MatchedScore { get; set; }
    public string? MatchedBy { get; set; }
    public string? FaqCategory { get; set; }
    public bool LlmEnabled { get; set; }
    public bool NeedsHandoff { get; set; }
    public string? ReplyText { get; set; }
    public long CreatedAt { get; set; }
}
