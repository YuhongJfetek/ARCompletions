using System;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.Extensions.Caching.Memory;

namespace ARCompletions.Services
{
    public class StateService : IStateService
    {
        private readonly ARCompletionsContext _db;
        private readonly IMemoryCache _cache;

        public StateService(ARCompletionsContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<BotConversationState?> GetStateAsync(string sourceType, string conversationId, bool useMemoryState)
        {
            var key = $"state:{sourceType}:{conversationId}";
            if (useMemoryState)
            {
                _cache.TryGetValue(key, out BotConversationState? s);
                return s;
            }
            return await _db.BotConversationStates.FindAsync(sourceType, conversationId);
        }

        public async Task SaveStateAsync(BotConversationState state, bool useMemoryState, string stateCacheKey)
        {
            if (useMemoryState)
            {
                _cache.Set(stateCacheKey, state, TimeSpan.FromHours(1));
            }
            else
            {
                if (_db.Entry(state).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
                {
                    _db.BotConversationStates.Add(state);
                }
                await _db.SaveChangesAsync();
            }
        }

        public async Task ClearPendingDisambiguationAtomicAsync(string sourceType, string conversationId, DateTimeOffset now)
        {
            using var tran = await _db.Database.BeginTransactionAsync();
            var dbState = await _db.BotConversationStates.FindAsync(sourceType, conversationId);
            if (dbState != null)
            {
                dbState.PendingDisambiguationIds = null;
                dbState.PendingDisambiguationRoute = null;
                dbState.PendingDisambiguationAt = null;
                dbState.UpdatedAt = now;
                await _db.SaveChangesAsync();
            }
            await tran.CommitAsync();
        }
    }
}
