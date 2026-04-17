using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;
using ARCompletions.Data;
using ARCompletions.Domain;

namespace ARCompletions.Services
{
    // QueryHintsService: detects preferred category keys from a query.
    // Behavior:
    // 1. Try to load a JSON mapping from BotConstantsConfigs key `bot.queryHints.mapping`.
    //    Expected format: { "keyword": "categoryKey", "term": "categoryKey", ... }
    // 2. If mapping exists, tokenized query tokens are looked up in mapping to yield category keys.
    // 3. If no mapping or no matches, fallback to simple frequency-based tokens as before.
    public class QueryHintsService : IQueryHintsService
    {
        private readonly ITextProcessingService _textProcessing;
        private readonly IBotConstantsService _botConstants;

        // local cache fallback (botConstants service already caches)
        private Dictionary<string, string>? _mappingCache;
        private DateTime _mappingLoadedAt = DateTime.MinValue;
        private readonly TimeSpan _mappingTtl = TimeSpan.FromSeconds(60);

        public QueryHintsService(ITextProcessingService textProcessing, IBotConstantsService botConstants)
        {
            _textProcessing = textProcessing;
            _botConstants = botConstants;
        }

        private async Task EnsureMappingLoadedAsync()
        {
            if (_mappingCache != null && (DateTime.UtcNow - _mappingLoadedAt) < _mappingTtl) return;
            try
            {
                var map = await _botConstants.GetQueryHintsMappingAsync().ConfigureAwait(false);
                if (map != null && map.Count > 0)
                {
                    _mappingCache = map;
                    _mappingLoadedAt = DateTime.UtcNow;
                    return;
                }
            }
            catch
            {
                // swallow and fallback
            }
            _mappingCache = null;
            _mappingLoadedAt = DateTime.UtcNow;
        }

        public string[]? DetectPreferredCategoryKeys(string normalizedText)
        {
            if (string.IsNullOrWhiteSpace(normalizedText)) return Array.Empty<string>();

            // ensure mapping loaded (synchronously wait brief since callers are sync)
            EnsureMappingLoadedAsync().GetAwaiter().GetResult();

            var tokens = _textProcessing.Tokenize(normalizedText ?? string.Empty) ?? Array.Empty<string>();
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (_mappingCache != null && _mappingCache.Count > 0)
            {
                foreach (var t in tokens)
                {
                    var key = t.Trim().ToLowerInvariant();
                    if (_mappingCache.TryGetValue(key, out var cat)) found.Add(cat);
                }
                if (found.Count > 0) return found.ToArray();
            }

            // fallback: return top 3 frequent tokens
            try
            {
                var preferred = tokens.GroupBy(t => t).OrderByDescending(g => g.Count()).Select(g => g.Key).Take(3).ToArray();
                return preferred;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}
