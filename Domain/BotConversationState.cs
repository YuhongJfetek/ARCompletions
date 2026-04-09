namespace ARCompletions.Domain;

public class BotConversationState
{
    public string SourceType { get; set; } = default!; // 'group' | 'room'
    public string ConversationId { get; set; } = default!;
    public System.DateTimeOffset? HandoffStartedAt { get; set; }
    public System.DateTimeOffset? HandoffUntil { get; set; }
    public System.Text.Json.JsonDocument? PendingDisambiguationIds { get; set; } // JSON array of faq_id (stored as json/jsonb)
    public string? PendingDisambiguationRoute { get; set; }
    public System.DateTimeOffset? PendingDisambiguationAt { get; set; }
    public System.DateTimeOffset? LastStaffMessageAt { get; set; }
    public System.DateTimeOffset UpdatedAt { get; set; } = System.DateTimeOffset.UtcNow;
}
