namespace ARCompletions.Domain;

public class BotMessageRoute
{
    public long RouteRowId { get; set; }
    public long? EventRowId { get; set; }
    public string? ConversationId { get; set; }
    public string? SourceType { get; set; }
    public string? LineUserId { get; set; }
    public string? LogEvent { get; set; }
    public string? Route { get; set; }
    public string? Reason { get; set; }
    public string? MatchedFaqId { get; set; }
    public double? MatchedScore { get; set; }
    public string? MatchedBy { get; set; }
    public string? FaqCategory { get; set; }
    public string? TopFaqIds { get; set; } // JSON array
    public string? AliasTerm { get; set; }
    public string? ReplyText { get; set; }
    public bool? LlmEnabled { get; set; }
    public bool? NeedsHumanHandoff { get; set; }
    public bool? IsStaffTriggered { get; set; }
    public int? ContextCountBefore { get; set; }
    public int? ContextCountAfter { get; set; }
    public string? LogClass { get; set; }
    public string? LogGroup { get; set; }
    public string? LogPriority { get; set; }
    public bool? LogUseful { get; set; }
    public System.DateTimeOffset CreatedAt { get; set; } = System.DateTimeOffset.UtcNow;
}
