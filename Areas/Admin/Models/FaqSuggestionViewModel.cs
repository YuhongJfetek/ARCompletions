using System;
using System.Collections.Generic;

namespace ARCompletions.Areas.Admin.Models;

public class FaqSuggestionViewModel
{
    public long EventRowId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string? SourceType { get; set; }
    public string? ConversationId { get; set; }
    public string? LineUserId { get; set; }

    public string? UserText { get; set; }
    public string? SuggestedAnswer { get; set; }
    public string? FaqCategory { get; set; }

    public string? Route { get; set; }
    public string? Reason { get; set; }
    public string? MatchedFaqId { get; set; }
    public double? MatchedScore { get; set; }

    public double? LlmConfidence { get; set; }
    public string? LlmModel { get; set; }
    public string? LlmReason { get; set; }
}
