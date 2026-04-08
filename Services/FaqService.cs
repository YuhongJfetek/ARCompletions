using System;
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
        private readonly ARCompletionsContext _db;
        private readonly IDbLogger _dbLogger;

        public FaqService(ARCompletionsContext db, IDbLogger dbLogger)
        {
            _db = db;
            _dbLogger = dbLogger;
        }

        public async Task<BotFaqItem?> FindExactAsync(string normalizedText)
        {
            await _dbLogger.LogAsync("Debug", "FindExactAsync start", new { Text = normalizedText });
            var faqs = await _db.BotFaqItems.AsNoTracking().Where(f => f.Enabled).ToListAsync();
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
            var faqs = await _db.BotFaqItems.AsNoTracking().Where(f => f.Enabled).ToListAsync();
            await _dbLogger.LogAsync("Debug", "FindEnabledFaqsAsync returned faqs", new { Count = faqs.Count });
            return faqs;
        }

        public async Task<List<BotFaqItem>> FindByIdsAsync(IEnumerable<string> ids)
        {
            var idList = ids?.ToList() ?? new List<string>();
            if (idList.Count == 0)
            {
                await _dbLogger.LogAsync("Debug", "FindByIdsAsync called with empty ids");
                return new List<BotFaqItem>();
            }
            await _dbLogger.LogAsync("Debug", "FindByIdsAsync lookup ids", new { Ids = idList });
            var faqs = await _db.BotFaqItems.AsNoTracking().Where(f => idList.Contains(f.FaqId)).ToListAsync();
            await _dbLogger.LogAsync("Debug", "FindByIdsAsync returned faqs", new { Count = faqs.Count });
            return faqs;
        }
    }
}
