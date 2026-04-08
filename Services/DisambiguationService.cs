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
    private readonly ARCompletionsContext _db;
    private readonly IMemoryCache _cache;
    public DisambiguationService(ARCompletionsContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<DisambiguationResult> TryHandleNumericSelectionAsync(BotConversationState? state, string normalizedText, string sourceType, string conversationId, DateTimeOffset now, bool useMemoryState)
    {
        var res = new DisambiguationResult { Handled = false };
        try
        {
            if (state == null || string.IsNullOrWhiteSpace(state.PendingDisambiguationIds)) return res;

            var pending = JsonSerializer.Deserialize<string[]>(state.PendingDisambiguationIds) ?? Array.Empty<string>();
            if (pending.Length == 0) return res;

            var mnum = Regex.Match(normalizedText ?? string.Empty, "\\d+");
            if (!mnum.Success || !int.TryParse(mnum.Value, out var selected)) return res;

            var idx = selected - 1;
            if (idx < 0 || idx >= pending.Length) return res;

            var chosenId = pending[idx];
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
        }
        catch (Exception ex)
        {
            try
            {
                var log = new AppLog
                {
                    Id = Guid.NewGuid().ToString(),
                    TimeStamp = DateTime.UtcNow,
                    Level = "Warning",
                    Message = "Disambiguation processing failed",
                    MessageTemplate = "Disambiguation processing failed for conversation",
                    Exception = ex.ToString(),
                    Properties = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new { ConversationId = conversationId }))
                };
                _db.AppLogs.Add(log);
                await _db.SaveChangesAsync();
            }
            catch
            {
                // swallow
            }
        }

        return res;
    }
}
