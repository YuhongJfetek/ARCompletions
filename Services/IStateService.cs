using System;
using System.Threading.Tasks;
using ARCompletions.Domain;

namespace ARCompletions.Services
{
    public interface IStateService
    {
        Task<BotConversationState?> GetStateAsync(string sourceType, string conversationId, bool useMemoryState);
        Task SaveStateAsync(BotConversationState state, bool useMemoryState, string stateCacheKey);
        Task ClearPendingDisambiguationAtomicAsync(string sourceType, string conversationId, DateTimeOffset now);
    }
}
