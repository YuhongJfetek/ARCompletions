using System;

namespace ARCompletions.Areas.Admin.Models;

public class ConversationSummaryViewModel
{
    public string? SourceType { get; set; }
    public string? ConversationId { get; set; }
    public string? LineUserId { get; set; }
    public DateTimeOffset LastReceivedAt { get; set; }
    public int MessageCount { get; set; }
    public string? LastUserText { get; set; }
}
