using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ARCompletions.Services
{
    public class CandidateBuilderService : ICandidateBuilderService
    {
        private readonly ARCompletionsContext _db;
        private readonly IDbLogger _dbLogger;
        private readonly IScoringService _scoring;

        public CandidateBuilderService(ARCompletionsContext db, IScoringService scoring, IDbLogger dbLogger)
        {
            _db = db;
            _scoring = scoring;
            _dbLogger = dbLogger;
        }

        public async Task<List<string>> BuildCandidatesAsync(string normalizedText, double[]? queryVec, string embeddingProvider, int topN = 5)
        {
            await _dbLogger.LogAsync("Debug", "BuildCandidatesAsync start", new { Len = (normalizedText ?? string.Empty).Length, HasVec = queryVec != null, Provider = embeddingProvider, TopN = topN });

            // load embedding items and faq map
            var embItems = await _db.BotFaqEmbeddings.AsNoTracking().Where(e => e.IsActive && e.EmbeddingProvider == embeddingProvider).ToListAsync();
            await _dbLogger.LogAsync("Debug", "Embeddings loaded", new { Count = embItems.Count });
            if (embItems.Count == 0)
            {
                await _dbLogger.LogAsync("Information", "No embedding items for provider", new { Provider = embeddingProvider });
                return new List<string>();
            }

            var faqIds = embItems.Select(e => e.FaqId).Distinct().ToList();
            var faqs = await _db.BotFaqItems.AsNoTracking().Where(f => faqIds.Contains(f.FaqId) && f.Enabled).ToListAsync();
            await _dbLogger.LogAsync("Debug", "FAQ details loaded", new { Count = faqs.Count });
            var faqMap = faqs.ToDictionary(f => f.FaqId, f => f);

            var vecMap = embItems.GroupBy(e => e.FaqId).ToDictionary(g => g.Key, g => g.SelectMany(x => x.Embedding ?? Array.Empty<double>()).ToArray());

            var candidateTuples = new List<(string FaqId, double[]? Vec, string Question, string SearchTextCache)>();
            foreach (var f in faqMap.Values)
            {
                double[]? v = null;
                var emb = embItems.FirstOrDefault(e => e.FaqId == f.FaqId && (e.Embedding?.Length ?? 0) > 0);
                if (emb != null) v = emb.Embedding;
                candidateTuples.Add((f.FaqId, v, f.Question ?? string.Empty, f.SearchTextCache ?? string.Empty));
            }

                await _dbLogger.LogAsync("Debug", "Candidate tuples prepared", new { Count = candidateTuples.Count });

            var scores = _scoring.ScoreCandidates(queryVec ?? Array.Empty<double>(), candidateTuples, normalizedText ?? string.Empty);
            var ranked = scores.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).Take(topN).ToList();
                await _dbLogger.LogAsync("Information", "Candidates built", new { ConversationTextLen = (normalizedText ?? string.Empty).Length, TopIds = ranked });
            return ranked;
        }
    }
}
