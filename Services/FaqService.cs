using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ARCompletions.Services
{
    public class FaqService : IFaqService
    {
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> _dbFactory;
        private readonly IDbLogger _dbLogger;

        public FaqService(Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> dbFactory, IDbLogger dbLogger)
        {
            _dbFactory = dbFactory;
            _dbLogger = dbLogger;
        }

        public async Task<BotFaqItem?> FindExactAsync(string normalizedText)
        {
            await _dbLogger.LogAsync("Debug", "FindExactAsync start", new { Text = normalizedText });
            using var db = _dbFactory.CreateDbContext();
            var faqs = await db.BotFaqItems.AsNoTracking().Where(f => f.Enabled).ToListAsync();
            await _dbLogger.LogAsync("Debug", "FindExactAsync loaded faqs", new { Count = faqs.Count });
            foreach (var f in faqs)
            {
                var q = (f.Question ?? string.Empty).Trim().ToLowerInvariant();
                if (q == normalizedText.Trim().ToLowerInvariant())
                {
                    await _dbLogger.LogAsync("Information", "FindExactAsync matched", new { FaqId = f.FaqId });
                    return f;
                }
            }
            await _dbLogger.LogAsync("Debug", "FindExactAsync no match for text");
            return null;
        }

        public async Task<List<BotFaqItem>> FindEnabledFaqsAsync()
        {
            using var db = _dbFactory.CreateDbContext();
            var faqs = await db.BotFaqItems.AsNoTracking().Where(f => f.Enabled).ToListAsync();
            await _dbLogger.LogAsync("Debug", "FindEnabledFaqsAsync returned faqs", new { Count = faqs.Count });
            return faqs;
        }

        public async Task<List<BotFaqItem>> FindByIdsAsync(IEnumerable<string> ids)
        {
            var sw = Stopwatch.StartNew();
            var idList = ids?.ToList() ?? new List<string>();
            if (idList.Count == 0)
            {
                await _dbLogger.LogAsync("Debug", "FindByIdsAsync called with empty ids");
                await _dbLogger.LogAsync("Debug", "FindByIdsAsync END", new { Ids = idList, ElapsedMs = sw.ElapsedMilliseconds });
                return new List<BotFaqItem>();
            }
            await _dbLogger.LogAsync("Debug", "FindByIdsAsync lookup ids", new { Ids = idList });
            using var db = _dbFactory.CreateDbContext();
            var faqs = await db.BotFaqItems.AsNoTracking().Where(f => idList.Contains(f.FaqId)).ToListAsync();
            await _dbLogger.LogAsync("Debug", "FindByIdsAsync returned faqs", new { Count = faqs.Count });
            await _dbLogger.LogAsync("Debug", "FindByIdsAsync END", new { Ids = idList, Count = faqs.Count, ElapsedMs = sw.ElapsedMilliseconds });
            return faqs;
        }
    }
}
