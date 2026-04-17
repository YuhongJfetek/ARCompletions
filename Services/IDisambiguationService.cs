using System;
using System.Threading.Tasks;
using ARCompletions.Domain;
using ARCompletions.Data;

namespace ARCompletions.Services;

public class DisambiguationResult
{
    public bool Handled { get; set; }
    public string? Route { get; set; }
    public string? MatchedFaqId { get; set; }
    public string? MatchedBy { get; set; }
    public double? Confidence { get; set; }
    public string? ReplyText { get; set; }
    public string? FaqCategory { get; set; }
    public bool NeedsHumanHandoff { get; set; }
}

public interface IDisambiguationService
{
    Task<DisambiguationResult> TryHandleNumericSelectionAsync(BotConversationState? state, string normalizedText, string sourceType, string conversationId, DateTimeOffset now, bool useMemoryState, ARCompletionsContext? db = null);
}
