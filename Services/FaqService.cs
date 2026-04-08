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
        private readonly ILogger<FaqService> _logger;

        public FaqService(ARCompletionsContext db, ILogger<FaqService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<BotFaqItem?> FindExactAsync(string normalizedText)
        {
            _logger?.LogDebug("FindExactAsync start: normalizedText={Text}", normalizedText);
            var faqs = await _db.BotFaqItems.AsNoTracking().Where(f => f.Enabled).ToListAsync();
            _logger?.LogDebug("FindExactAsync loaded faqs: count={Count}", faqs.Count);
            foreach (var f in faqs)
            {
                var q = (f.Question ?? string.Empty).Trim().ToLowerInvariant();
                if (q == normalizedText.Trim().ToLowerInvariant())
                {
                    _logger?.LogInformation("FindExactAsync matched FaqId={FaqId}", f.FaqId);
                    return f;
                }
            }
            _logger?.LogDebug("FindExactAsync no match for text");
            return null;
        }

        public async Task<List<BotFaqItem>> FindEnabledFaqsAsync()
        {
            var faqs = await _db.BotFaqItems.AsNoTracking().Where(f => f.Enabled).ToListAsync();
            _logger?.LogDebug("FindEnabledFaqsAsync returned count={Count}", faqs.Count);
            return faqs;
        }

        public async Task<List<BotFaqItem>> FindByIdsAsync(IEnumerable<string> ids)
        {
            var idList = ids?.ToList() ?? new List<string>();
            if (idList.Count == 0)
            {
                _logger?.LogDebug("FindByIdsAsync called with empty ids");
                return new List<BotFaqItem>();
            }
            _logger?.LogDebug("FindByIdsAsync lookup ids={Ids}", string.Join(',', idList));
            var faqs = await _db.BotFaqItems.AsNoTracking().Where(f => idList.Contains(f.FaqId)).ToListAsync();
            _logger?.LogDebug("FindByIdsAsync returned count={Count}", faqs.Count);
            return faqs;
        }
    }
}
