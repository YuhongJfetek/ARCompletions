namespace ARCompletions.Domain;

public class FaqCandidate
{
    public string Id { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public string AnalysisJobId { get; set; } = default!;
    public string Question { get; set; } = default!;
    public string Answer { get; set; } = default!;
    public string? SuggestedCategory { get; set; }
    public string? SuggestedTags { get; set; }
    public double? ConfidenceScore { get; set; }
    public string Status { get; set; } = default!;
    public string? SourceConversationIds { get; set; }
    public string? AiModel { get; set; }
    public string? PromptVersion { get; set; }
    public string? AiSuggestionJson { get; set; }
    public long GeneratedAt { get; set; }
    public string? ReviewedByType { get; set; }
    public string? ReviewedById { get; set; }
    public long? ReviewedAt { get; set; }
    public long CreatedAt { get; set; }
    public long? UpdatedAt { get; set; }
}
