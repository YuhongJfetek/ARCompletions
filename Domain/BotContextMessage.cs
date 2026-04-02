namespace ARCompletions.Domain;

public class BotContextMessage
{
    public long MessageId { get; set; }
    public string ConversationId { get; set; } = default!;
    public string Role { get; set; } = default!; // 'user' | 'assistant'
    public string Text { get; set; } = default!;
    public long Ts { get; set; } // epoch ms
    public string? MetaRoute { get; set; }
    public string? MetaMatchedFaqId { get; set; }
    public string? MetaCandidates { get; set; } // JSON array
    public string? MetaAliasTerm { get; set; }
    public System.DateTimeOffset CreatedAt { get; set; } = System.DateTimeOffset.UtcNow;
}
