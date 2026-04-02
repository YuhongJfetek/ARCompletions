namespace ARCompletions.Domain;

public class BotFaqItem
{
    public string FaqId { get; set; } = default!;
    public string Question { get; set; } = default!;
    public string Answer { get; set; } = default!;
    public string? Category { get; set; }
    public string? CategoryKey { get; set; }
    public string? Subcategory { get; set; }
    // JSONB columns stored as JSON string
    public string? Keywords { get; set; }
    public string? QueryExamples { get; set; }
    public string? AliasTerms { get; set; }
    public string? Sources { get; set; }
    public bool NeedsHumanHandoff { get; set; } = false;
    public bool Enabled { get; set; } = true;
    public string? SearchTextCache { get; set; }
    public System.DateTimeOffset CreatedAt { get; set; } = System.DateTimeOffset.UtcNow;
    public System.DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
