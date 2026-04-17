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
        private readonly IBufferedAppLogger? _bufferedLogger;

        public FaqService(Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> dbFactory, IDbLogger dbLogger, IBufferedAppLogger? bufferedLogger = null)
        {
            _dbFactory = dbFactory;
            _dbLogger = dbLogger;
            _bufferedLogger = bufferedLogger;
        }
        public async Task<BotFaqItem?> FindExactAsync(string normalizedText, ARCompletionsContext? db = null)
        {
            var searchKey = normalizedText?.Trim().ToLowerInvariant() ?? string.Empty;
            if (db != null)
            {
                await _dbLogger.LogAsync(db, "Debug", "FindExactAsync start", new { Text = normalizedText });
                var found = await EfCompiledQueries.FindExactBySearchText(db, searchKey);
                if (found != null)
                {
                    await _dbLogger.LogAsync(db, "Information", "FindExactAsync matched", new { FaqId = found.FaqId });
                    return found;
                }
                await _dbLogger.LogAsync(db, "Debug", "FindExactAsync no match for text");
                return null;
            }

            if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "FindExactAsync start", new { Text = normalizedText });
            using var _db = _dbFactory.CreateDbContext();
            var found2 = await EfCompiledQueries.FindExactBySearchText(_db, searchKey);
            if (found2 != null)
            {
                if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Information", "FindExactAsync matched", new { FaqId = found2.FaqId });
                return found2;
            }
            if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "FindExactAsync no match for text");
            return null;
        }

        public async Task<List<BotFaqItem>> FindByIdsAsync(IEnumerable<string> ids, ARCompletionsContext? db = null)
        {
            var sw = Stopwatch.StartNew();
            var idList = ids?.ToList() ?? new List<string>();
            if (idList.Count == 0)
            {
                if (db != null) await _dbLogger.LogAsync(db, "Debug", "FindByIdsAsync called with empty ids"); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "FindByIdsAsync called with empty ids");
                if (db != null) await _dbLogger.LogAsync(db, "Debug", "FindByIdsAsync END", new { Ids = idList, ElapsedMs = sw.ElapsedMilliseconds }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "FindByIdsAsync END", new { Ids = idList, ElapsedMs = sw.ElapsedMilliseconds });
                return new List<BotFaqItem>();
            }

            if (db == null)
            {
                if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "FindByIdsAsync lookup ids", new { Ids = idList });
                using var _db = _dbFactory.CreateDbContext();
                var faqs = await EfCompiledQueries.FindFaqsByIds(_db, idList);
                if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "FindByIdsAsync returned faqs", new { Count = faqs.Count });
                if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "FindByIdsAsync END", new { Ids = idList, Count = faqs.Count, ElapsedMs = sw.ElapsedMilliseconds });
                return faqs;
            }

            await _dbLogger.LogAsync(db, "Debug", "FindByIdsAsync lookup ids", new { Ids = idList });
            var faqs2 = await EfCompiledQueries.FindFaqsByIds(db, idList);
            await _dbLogger.LogAsync(db, "Debug", "FindByIdsAsync returned faqs", new { Count = faqs2.Count });
            await _dbLogger.LogAsync(db, "Debug", "FindByIdsAsync END", new { Ids = idList, Count = faqs2.Count, ElapsedMs = sw.ElapsedMilliseconds });
            return faqs2;
        }
    }
}
