namespace ARCompletions.Domain;

public class ConversationState
{
    public string Id { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public string SourceType { get; set; } = default!; // 'group' | 'room'
    public string ConversationId { get; set; } = default!;
    public bool BotEnabled { get; set; } = true;
    public long? HandoffUntil { get; set; }
    public string? PendingDisambiguationIds { get; set; } // JSON array
    public long? PendingDisambiguationAt { get; set; }
    public long? LastStaffMessageAt { get; set; }
    public long UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
