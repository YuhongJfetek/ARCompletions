using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Services
{
    internal class CacheEntry
    {
        public DateTimeOffset LoadedAt { get; set; }
        public List<BotFaqEmbedding> Embeddings { get; set; } = new();
        public Dictionary<string, BotFaqItem> FaqMap { get; set; } = new();
    }

    public class EmbeddingsCache : IEmbeddingsCache
    {
        private readonly IServiceProvider _services;
        private readonly ConcurrentDictionary<string, (CacheEntry Entry, SemaphoreSlim Lock)> _store = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _ttl;

        public EmbeddingsCache(IServiceProvider services)
        {
            _services = services;
            var ttlSeconds = int.TryParse(Environment.GetEnvironmentVariable("EMBEDDINGS_CACHE_TTL_SECONDS"), out var s) ? s : 600;
            _ttl = TimeSpan.FromSeconds(Math.Max(30, ttlSeconds));
        }

        public async Task<(List<BotFaqEmbedding> Embeddings, Dictionary<string, BotFaqItem> FaqMap)> GetOrLoadAsync(string provider)
        {
            provider ??= string.Empty;
            var tuple = _store.GetOrAdd(provider, _ => (new CacheEntry(), new SemaphoreSlim(1, 1)));
            var entry = tuple.Entry;
            if (entry.Embeddings.Count > 0 && DateTimeOffset.UtcNow - entry.LoadedAt < _ttl)
            {
                return (entry.Embeddings, entry.FaqMap);
            }

            await tuple.Lock.WaitAsync();
            try
            {
                // double-check
                if (entry.Embeddings.Count > 0 && DateTimeOffset.UtcNow - entry.LoadedAt < _ttl)
                {
                    return (entry.Embeddings, entry.FaqMap);
                }

                // create a scope to get a DbContext
                using var scope = _services.CreateScope();
                var sp = scope.ServiceProvider;
                var db = sp.GetRequiredService<ARCompletionsContext>();

                var embItems = await db.BotFaqEmbeddings.AsNoTracking()
                    .Where(e => e.IsActive && e.EmbeddingProvider == provider)
                    .ToListAsync();

                var faqIds = embItems.Select(e => e.FaqId).Distinct().ToList();
                var faqs = await db.BotFaqItems.AsNoTracking().Where(f => faqIds.Contains(f.FaqId) && f.Enabled).ToListAsync();

                entry.Embeddings = embItems;
                entry.FaqMap = faqs.ToDictionary(f => f.FaqId, f => f);
                entry.LoadedAt = DateTimeOffset.UtcNow;
                return (entry.Embeddings, entry.FaqMap);
            }
            finally
            {
                tuple.Lock.Release();
            }
        }

        public async Task RefreshAsync(string provider)
        {
            provider ??= string.Empty;
            var tuple = _store.GetOrAdd(provider, _ => (new CacheEntry(), new SemaphoreSlim(1, 1)));
            await tuple.Lock.WaitAsync();
            try
            {
                tuple.Entry.Embeddings.Clear();
                tuple.Entry.FaqMap.Clear();
                tuple.Entry.LoadedAt = DateTimeOffset.MinValue;
            }
            finally { tuple.Lock.Release(); }
            // trigger a reload
            await GetOrLoadAsync(provider);
        }
    }
}
