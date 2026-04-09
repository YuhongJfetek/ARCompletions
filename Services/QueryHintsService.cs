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
        private readonly ARCompletionsContext _db;

        // cached mapping to avoid DB hit on every request
        private Dictionary<string, string>? _mappingCache;
        private DateTime _mappingLoadedAt = DateTime.MinValue;
        private readonly TimeSpan _mappingTtl = TimeSpan.FromSeconds(60);

        public QueryHintsService(ITextProcessingService textProcessing, ARCompletionsContext db)
        {
            _textProcessing = textProcessing;
            _db = db;
        }

        private void EnsureMappingLoaded()
        {
            if (_mappingCache != null && (DateTime.UtcNow - _mappingLoadedAt) < _mappingTtl) return;
            try
            {
                var cfg = _db.BotConstantsConfigs.AsNoTracking().FirstOrDefault(c => c.ConfigKey == "bot.queryHints.mapping");
                if (cfg != null && !string.IsNullOrWhiteSpace(cfg.ConfigValue))
                {
                    try
                    {
                        var doc = System.Text.Json.JsonDocument.Parse(cfg.ConfigValue!);
                        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            foreach (var p in doc.RootElement.EnumerateObject())
                            {
                                var key = p.Name.Trim().ToLowerInvariant();
                                var val = p.Value.GetString() ?? string.Empty;
                                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(val)) map[key] = val.Trim();
                            }
                        }
                        _mappingCache = map;
                        _mappingLoadedAt = DateTime.UtcNow;
                        return;
                    }
                    catch
                    {
                        // parse failed: fall through to clear mapping
                    }
                }
            }
            catch
            {
                // swallow DB errors and fallback
            }
            _mappingCache = null;
            _mappingLoadedAt = DateTime.UtcNow;
        }

        public string[]? DetectPreferredCategoryKeys(string normalizedText)
        {
            if (string.IsNullOrWhiteSpace(normalizedText)) return Array.Empty<string>();

            EnsureMappingLoaded();

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
