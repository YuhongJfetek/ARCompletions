using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ARCompletions.Services
{
    public interface IBotConstantsService
    {
        Task<Dictionary<string, string>?> GetQueryHintsMappingAsync();
        Task<List<BotConstantsConfig>> GetAllConfigsAsync();
        void Invalidate(string key = null);
    }

    public class BotConstantsService : IBotConstantsService
    {
        const string Key_mapping = "bot.queryHints.mapping";
        const string CacheKey_QueryHints = "BotConstants_QueryHintsMapping";

        readonly Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> _dbFactory;
        readonly IMemoryCache _cache;

        public BotConstantsService(Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> dbFactory, IMemoryCache cache)
        {
            _dbFactory = dbFactory;
            _cache = cache;
        }

        public async Task<Dictionary<string, string>?> GetQueryHintsMappingAsync()
        {
            return await _cache.GetOrCreateAsync(CacheKey_QueryHints, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
                try
                {
                    using var db = _dbFactory.CreateDbContext();
                    var cfg = await db.BotConstantsConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.ConfigKey == Key_mapping).ConfigureAwait(false);
                    if (cfg == null || string.IsNullOrWhiteSpace(cfg.ConfigValue)) return null;
                    try
                    {
                        var doc = System.Text.Json.JsonDocument.Parse(cfg.ConfigValue!);
                        if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
                        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var p in doc.RootElement.EnumerateObject())
                        {
                            var k = p.Name.Trim().ToLowerInvariant();
                            var v = p.Value.GetString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(k) && !string.IsNullOrWhiteSpace(v)) map[k] = v.Trim();
                        }
                        return map;
                    }
                    catch
                    {
                        return null;
                    }
                }
                catch
                {
                    return null;
                }
            }).ConfigureAwait(false);
        }

        public async Task<List<BotConstantsConfig>> GetAllConfigsAsync()
        {
            try
            {
                using var db = _dbFactory.CreateDbContext();
                return await db.BotConstantsConfigs.AsNoTracking().ToListAsync().ConfigureAwait(false);
            }
            catch
            {
                return new List<BotConstantsConfig>();
            }
        }

        public void Invalidate(string key = null)
        {
            if (string.IsNullOrEmpty(key) || key == Key_mapping)
                _cache.Remove(CacheKey_QueryHints);
        }
    }
}
