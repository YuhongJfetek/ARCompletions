using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ARCompletions.Domain;
using ARCompletions.Data;

namespace ARCompletions.Services
{
    public interface IFaqService
    {
        Task<BotFaqItem?> FindExactAsync(string normalizedText, ARCompletionsContext? db = null);
        Task<List<BotFaqItem>> FindEnabledFaqsAsync(ARCompletionsContext? db = null);
        Task<List<BotFaqItem>> FindByIdsAsync(IEnumerable<string> ids, ARCompletionsContext? db = null);
    }
}
