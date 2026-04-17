using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ARCompletions.Services;

public class DisambiguationService : IDisambiguationService
{
    private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> _dbFactory;
    private readonly IMemoryCache _cache;
    private readonly IDbLogger _dbLogger;
    private readonly IBufferedAppLogger? _bufferedLogger;

    public DisambiguationService(Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> dbFactory, IMemoryCache cache, IDbLogger dbLogger, IBufferedAppLogger? bufferedLogger = null)
    {
        _dbFactory = dbFactory;
        _cache = cache;
        _dbLogger = dbLogger;
        _bufferedLogger = bufferedLogger;
    }

    public async Task<DisambiguationResult> TryHandleNumericSelectionAsync(BotConversationState? state, string normalizedText, string sourceType, string conversationId, DateTimeOffset now, bool useMemoryState, ARCompletionsContext? db = null)
    {
        var res = new DisambiguationResult { Handled = false };
        try
        {
            if (state == null || state.PendingDisambiguationIds == null) return res;

            var pending = JsonSerializer.Deserialize<string[]>(state.PendingDisambiguationIds.RootElement.GetRawText()) ?? Array.Empty<string>();
            if (pending.Length == 0) return res;

            var mnum = Regex.Match(normalizedText ?? string.Empty, "\\d+");
            if (!mnum.Success || !int.TryParse(mnum.Value, out var selected)) return res;

            var idx = selected - 1;
            if (idx < 0 || idx >= pending.Length) return res;

            var chosenId = pending[idx];

            if (db == null)
            {
                using var _db = _dbFactory.CreateDbContext();
                var faq = await _db.BotFaqItems.AsNoTracking().FirstOrDefaultAsync(f => f.FaqId == chosenId && f.Enabled);
                if (faq == null) return res;

                res.Handled = true;
                res.Route = "faq";
                res.MatchedFaqId = faq.FaqId;
                res.MatchedBy = "disambiguation_selection";
                res.Confidence = 1.0;
                res.ReplyText = faq.Answer;
                res.FaqCategory = faq.CategoryKey ?? faq.Category;
                res.NeedsHumanHandoff = faq.NeedsHumanHandoff;

                // clear pending disambiguation atomically
                state.PendingDisambiguationIds = null;
                state.PendingDisambiguationRoute = null;
                state.PendingDisambiguationAt = null;
                state.UpdatedAt = now;

                if (useMemoryState)
                {
                    var cacheKey = $"state:{sourceType}:{conversationId}";
                    _cache.Set(cacheKey, state, TimeSpan.FromHours(1));
                }
                else
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

                return res;
            }

            var faq2 = await db.BotFaqItems.AsNoTracking().FirstOrDefaultAsync(f => f.FaqId == chosenId && f.Enabled);
            if (faq2 == null) return res;

            res.Handled = true;
            res.Route = "faq";
            res.MatchedFaqId = faq2.FaqId;
            res.MatchedBy = "disambiguation_selection";
            res.Confidence = 1.0;
            res.ReplyText = faq2.Answer;
            res.FaqCategory = faq2.CategoryKey ?? faq2.Category;
            res.NeedsHumanHandoff = faq2.NeedsHumanHandoff;

            // clear pending disambiguation using provided db
            state.PendingDisambiguationIds = null;
            state.PendingDisambiguationRoute = null;
            state.PendingDisambiguationAt = null;
            state.UpdatedAt = now;

            if (useMemoryState)
            {
                var cacheKey = $"state:{sourceType}:{conversationId}";
                _cache.Set(cacheKey, state, TimeSpan.FromHours(1));
            }
            else
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
            }
        }
        catch (Exception ex)
        {
            try
            {
                if (db != null) await _dbLogger.LogAsync(db, "Warning", "Disambiguation processing failed", new { ConversationId = conversationId }, ex);
                else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Warning", "Disambiguation processing failed", new { ConversationId = conversationId });
            }
            catch
            {
                // swallow
            }
        }

        return res;
    }
}
