using System;
using ARCompletions.Domain;
using ARCompletions.Controllers.Api;

namespace ARCompletions.Services
{
    public interface IResponseBuilder
    {
        BotQueryResponse BuildResponse(string route, string? matchedFaqId, string? matchedBy, double? confidence, string? replyText, string replyMode, object[] quickReplies, BotConversationState state, bool botEnabled, DateTimeOffset? handoffUntil, string? faqCategory, bool needsHumanHandoff, int contextBefore = 0, int contextAfter = 0);
    }
}
