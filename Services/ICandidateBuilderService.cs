using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ARCompletions.Services
{
    public interface ICandidateBuilderService
    {
        Task<List<string>> BuildCandidatesAsync(string normalizedText, double[]? queryVec, string embeddingProvider, int topN = 5, ARCompletions.Data.ARCompletionsContext? db = null);
    }
}
