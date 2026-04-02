namespace ARCompletions.Domain;

public class BotFaqAlias
{
    public System.Guid AliasId { get; set; }
    public string Term { get; set; } = default!;
    public string? Synonyms { get; set; } // JSON array
    public string Mode { get; set; } = "disambiguation"; // 'direct' | 'disambiguation'
    public string FaqIds { get; set; } = default!; // JSON array of faq_id
    public bool Enabled { get; set; } = true;
    public System.DateTimeOffset CreatedAt { get; set; } = System.DateTimeOffset.UtcNow;
    public System.DateTimeOffset? UpdatedAt { get; set; }
}
