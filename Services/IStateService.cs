using System;
using System.Threading.Tasks;
using ARCompletions.Domain;
using ARCompletions.Data;

namespace ARCompletions.Services
{
    public interface IStateService
    {
        Task<BotConversationState?> GetStateAsync(string sourceType, string conversationId, bool useMemoryState, ARCompletionsContext? db = null);
        Task SaveStateAsync(BotConversationState state, bool useMemoryState, string stateCacheKey, bool deferSave = false, ARCompletionsContext? db = null);
        Task ClearPendingDisambiguationAtomicAsync(string sourceType, string conversationId, DateTimeOffset now, ARCompletionsContext? db = null);
    }
}
