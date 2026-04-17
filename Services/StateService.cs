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

        public async Task<BotConversationState?> GetStateAsync(string sourceType, string conversationId, bool useMemoryState, ARCompletionsContext? db = null)
        {
            var key = $"state:{sourceType}:{conversationId}";
            if (useMemoryState)
            {
                _cache.TryGetValue(key, out BotConversationState? s);
                return s;
            }
            if (db != null)
            {
                return await db.BotConversationStates.FindAsync(sourceType, conversationId);
            }
            using var _db = _dbFactory.CreateDbContext();
            return await _db.BotConversationStates.FindAsync(sourceType, conversationId);
        }

        public async Task SaveStateAsync(BotConversationState state, bool useMemoryState, string stateCacheKey, bool deferSave = false, ARCompletionsContext? db = null)
        {
            if (useMemoryState)
            {
                _cache.Set(stateCacheKey, state, TimeSpan.FromHours(1));
            }
            else
            {
                if (db != null)
                {
                    if (db.Entry(state).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
                    {
                        db.BotConversationStates.Add(state);
                    }
                    if (!deferSave)
                    {
                        await db.SaveChangesAsync();
                    }
                    return;
                }
                using var _db = _dbFactory.CreateDbContext();
                if (_db.Entry(state).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
                {
                    _db.BotConversationStates.Add(state);
                }
                if (!deferSave)
                {
                    await _db.SaveChangesAsync();
                }
            }
        }

        public async Task ClearPendingDisambiguationAtomicAsync(string sourceType, string conversationId, DateTimeOffset now, ARCompletionsContext? db = null)
        {
            if (db != null)
            {
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
                return;
            }
            using var _db = _dbFactory.CreateDbContext();
            using var _tran = await _db.Database.BeginTransactionAsync();
            var state = await _db.BotConversationStates.FindAsync(sourceType, conversationId);
            if (state != null)
            {
                state.PendingDisambiguationIds = null;
                state.PendingDisambiguationRoute = null;
                state.PendingDisambiguationAt = null;
                state.UpdatedAt = now;
                await _db.SaveChangesAsync();
            }
            await _tran.CommitAsync();
        }
    }
}
