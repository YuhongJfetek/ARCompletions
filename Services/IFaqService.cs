using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ARCompletions.Domain;

namespace ARCompletions.Services
{
    public interface IFaqService
    {
        Task<BotFaqItem?> FindExactAsync(string normalizedText);
        Task<List<BotFaqItem>> FindEnabledFaqsAsync();
        Task<List<BotFaqItem>> FindByIdsAsync(IEnumerable<string> ids);
    }
}
