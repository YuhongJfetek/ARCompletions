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
        private readonly IScoringService _scoring;
        private readonly ILogger<CandidateBuilderService> _logger;

        public CandidateBuilderService(ARCompletionsContext db, IScoringService scoring, ILogger<CandidateBuilderService> logger)
        {
            _db = db;
            _scoring = scoring;
            _logger = logger;
        }

        public async Task<List<string>> BuildCandidatesAsync(string normalizedText, double[]? queryVec, string embeddingProvider, int topN = 5)
        {
            _logger?.LogDebug("BuildCandidatesAsync start: TextLen={Len} HasQueryVec={HasVec} Provider={Provider} TopN={TopN}", (normalizedText ?? string.Empty).Length, queryVec != null, embeddingProvider, topN);

            // load embedding items and faq map
            var embItems = await _db.BotFaqEmbeddings.AsNoTracking().Where(e => e.IsActive && e.EmbeddingProvider == embeddingProvider).ToListAsync();
            _logger?.LogDebug("Embeddings loaded: count={Count}", embItems.Count);
            if (embItems.Count == 0)
            {
                _logger?.LogInformation("No embedding items for provider={Provider}", embeddingProvider);
                return new List<string>();
            }

            var faqIds = embItems.Select(e => e.FaqId).Distinct().ToList();
            var faqs = await _db.BotFaqItems.AsNoTracking().Where(f => faqIds.Contains(f.FaqId) && f.Enabled).ToListAsync();
            _logger?.LogDebug("FAQ details loaded: count={Count}", faqs.Count);
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

            _logger?.LogDebug("Candidate tuples prepared: count={Count}", candidateTuples.Count);

            var scores = _scoring.ScoreCandidates(queryVec ?? Array.Empty<double>(), candidateTuples, normalizedText ?? string.Empty);
            var ranked = scores.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).Take(topN).ToList();
            _logger?.LogInformation("Candidates built: ConversationTextLen={Len} TopIds={Ids}", (normalizedText ?? string.Empty).Length, string.Join(',', ranked));
            return ranked;
        }
    }
}
