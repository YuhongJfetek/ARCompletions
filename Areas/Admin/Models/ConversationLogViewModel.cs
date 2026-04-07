using System;
using System.Collections.Generic;

namespace ARCompletions.Areas.Admin.Models;

public class ConversationLogViewModel
{
    public string SourceType { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string? LineUserId { get; set; }

    public IReadOnlyList<ConversationLogMessageViewModel> Messages { get; set; } = Array.Empty<ConversationLogMessageViewModel>();
}

public class ConversationLogMessageViewModel
{
    public long EventRowId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string? MessageType { get; set; }
    public string? EventType { get; set; }
    public string? RawEventJson { get; set; }

    public string? Route { get; set; }
    public string? Reason { get; set; }
    public string? FaqCategory { get; set; }
    public string? MatchedFaqId { get; set; }
    public double? MatchedScore { get; set; }
    public string? ReplyText { get; set; }

    public double? Confidence { get; set; }
    public string? LlmModel { get; set; }
    public string? LlmReason { get; set; }
}
