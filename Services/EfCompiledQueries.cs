using System.Collections.Generic;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Services
{
    public static class EfCompiledQueries
    {
        // Compiled queries for hot paths
        // NOTE: EF Core doesn't support CompileAsyncQuery with array/collection parameters using Contains.
        // Replace the compiled query with a normal async method for FindFaqsByIds.
        public static Task<List<BotFaqItem>> FindFaqsByIds(ARCompletionsContext db, string[] ids)
            => db.BotFaqItems.AsNoTracking().Where(f => ids.Contains(f.FaqId)).ToListAsync();

        public static readonly Func<ARCompletionsContext, Task<List<BotConstantsConfig>>> GetBotConstantsConfigs
            = EF.CompileAsyncQuery((ARCompletionsContext db)
                => db.BotConstantsConfigs.AsNoTracking().ToList());

        public static readonly Func<ARCompletionsContext, string, Task<BotFaqItem?>> FindExactBySearchText
            = EF.CompileAsyncQuery((ARCompletionsContext db, string normalized)
                => db.BotFaqItems.AsNoTracking().FirstOrDefault(f => f.Enabled && f.SearchTextCache == normalized));
    }
}
