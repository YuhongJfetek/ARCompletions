using System;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.Extensions.Caching.Memory;

namespace ARCompletions.Services
{
    public class StateService : IStateService
    {
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> _dbFactory;
        private readonly IMemoryCache _cache;

        public StateService(Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> dbFactory, IMemoryCache cache)
        {
            _dbFactory = dbFactory;
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
            using var db = _dbFactory.CreateDbContext();
            return await db.BotConversationStates.FindAsync(sourceType, conversationId);
        }

        public async Task SaveStateAsync(BotConversationState state, bool useMemoryState, string stateCacheKey, bool deferSave = false)
        {
            if (useMemoryState)
            {
                _cache.Set(stateCacheKey, state, TimeSpan.FromHours(1));
            }
            else
            {
                using var db = _dbFactory.CreateDbContext();
                if (db.Entry(state).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
                {
                    db.BotConversationStates.Add(state);
                }
                if (!deferSave)
                {
                    await db.SaveChangesAsync();
                }
            }
        }

        public async Task ClearPendingDisambiguationAtomicAsync(string sourceType, string conversationId, DateTimeOffset now)
        {
            using var db = _dbFactory.CreateDbContext();
            using var tran = await db.Database.BeginTransactionAsync();
            var dbState = await db.BotConversationStates.FindAsync(sourceType, conversationId);
            if (dbState != null)
            {
                dbState.PendingDisambiguationIds = null;
                dbState.PendingDisambiguationRoute = null;
                dbState.PendingDisambiguationAt = null;
                dbState.UpdatedAt = now;
                await db.SaveChangesAsync();
            }
            await tran.CommitAsync();
        }
    }
}
