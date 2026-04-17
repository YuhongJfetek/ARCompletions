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
        // Use concrete array parameter so EF can translate Contains correctly
        public static readonly Func<ARCompletionsContext, string[], Task<List<BotFaqItem>>> FindFaqsByIds
            = EF.CompileAsyncQuery((ARCompletionsContext db, string[] ids)
                => db.BotFaqItems.AsNoTracking().Where(f => ids.Contains(f.FaqId)).ToList());

        public static readonly Func<ARCompletionsContext, Task<List<BotConstantsConfig>>> GetBotConstantsConfigs
            = EF.CompileAsyncQuery((ARCompletionsContext db)
                => db.BotConstantsConfigs.AsNoTracking().ToList());

        public static readonly Func<ARCompletionsContext, string, Task<BotFaqItem?>> FindExactBySearchText
            = EF.CompileAsyncQuery((ARCompletionsContext db, string normalized)
                => db.BotFaqItems.AsNoTracking().FirstOrDefault(f => f.Enabled && f.SearchTextCache == normalized));
    }
}
