using System;
using ARCompletions.Domain;
using ARCompletions.Controllers.Api;

namespace ARCompletions.Services
{
    public class ResponseBuilder : IResponseBuilder
    {
        public BotQueryResponse BuildResponse(string route, string? matchedFaqId, string? matchedBy, double? confidence, string? replyText, string replyMode, object[] quickReplies, BotConversationState state, bool botEnabled, DateTimeOffset? handoffUntil, string? faqCategory, bool needsHumanHandoff, int contextBefore = 0, int contextAfter = 0)
        {
            var shouldReply = route == "faq" || (route == "candidates" && quickReplies.Length > 0);
            return new BotQueryResponse
            {
                ShouldReply = shouldReply,
                Route = route,
                MatchedFaqId = matchedFaqId,
                MatchedBy = matchedBy,
                Confidence = confidence,
                ReplyText = replyText,
                ReplyMode = replyMode,
                QuickReplyItems = quickReplies,
                StateChanges = new BotQueryStateChanges
                {
                    BotEnabled = botEnabled,
                    HandoffUntil = handoffUntil,
                    PendingDisambiguationIds = route == "candidates" && quickReplies.Length > 0 ? (state?.PendingDisambiguationIds != null ? System.Text.Json.JsonSerializer.Deserialize<string[]>(state.PendingDisambiguationIds.RootElement.GetRawText()) ?? Array.Empty<string>() : Array.Empty<string>()) : Array.Empty<string>(),
                    PendingDisambiguationRoute = route == "candidates" && quickReplies.Length > 0 ? "faq" : null
                },
                LogPayload = new BotQueryLogPayload
                {
                    FaqCategory = faqCategory,
                    LlmEnabled = false,
                    NeedsHumanHandoff = needsHumanHandoff,
                    IsStaffTriggered = false,
                    ContextCountBefore = contextBefore,
                    ContextCountAfter = contextAfter
                }
            };
        }
    }
}
