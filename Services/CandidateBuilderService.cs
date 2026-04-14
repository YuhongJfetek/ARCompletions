using System;
using System.Diagnostics;
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
            var sw = Stopwatch.StartNew();
            await _dbLogger.LogAsync("Debug", "BuildCandidatesAsync start", new { Len = (normalizedText ?? string.Empty).Length, HasVec = queryVec != null, Provider = embeddingProvider, TopN = topN });

            try
            {
                // load embedding items and faq map
                var t0 = sw.ElapsedMilliseconds;
                var embItems = await _db.BotFaqEmbeddings.AsNoTracking().Where(e => e.IsActive && e.EmbeddingProvider == embeddingProvider).ToListAsync();
                var t1 = sw.ElapsedMilliseconds;
                await _dbLogger.LogAsync("Debug", "Embeddings loaded", new { Count = embItems.Count, ElapsedMs = t1 - t0 });
                if (embItems.Count == 0)
                {
                    await _dbLogger.LogAsync("Information", "No embedding items for provider", new { Provider = embeddingProvider });
                    return new List<string>();
                }

                var t2Start = sw.ElapsedMilliseconds;
                var faqIds = embItems.Select(e => e.FaqId).Distinct().ToList();
                var faqs = await _db.BotFaqItems.AsNoTracking().Where(f => faqIds.Contains(f.FaqId) && f.Enabled).ToListAsync();
                var t2 = sw.ElapsedMilliseconds;
                await _dbLogger.LogAsync("Debug", "FAQ details loaded", new { Count = faqs.Count, ElapsedMs = t2 - t2Start });
                var faqMap = faqs.ToDictionary(f => f.FaqId, f => f);

                var t3Start = sw.ElapsedMilliseconds;
                var candidateTuples = new List<(string FaqId, double[]? Vec, string Question, string SearchTextCache)>();
                foreach (var f in faqMap.Values)
                {
                    double[]? v = null;
                    var emb = embItems.FirstOrDefault(e => e.FaqId == f.FaqId && (e.Embedding?.Length ?? 0) > 0);
                    if (emb != null) v = emb.Embedding;
                    candidateTuples.Add((f.FaqId, v, f.Question ?? string.Empty, f.SearchTextCache ?? string.Empty));
                }
                var t3 = sw.ElapsedMilliseconds;
                await _dbLogger.LogAsync("Debug", "Candidate tuples prepared", new { Count = candidateTuples.Count, ElapsedMs = t3 - t3Start });

                var t4Start = sw.ElapsedMilliseconds;
                var scores = _scoring.ScoreCandidates(queryVec ?? Array.Empty<double>(), candidateTuples, normalizedText ?? string.Empty, faqMap);
                var t4 = sw.ElapsedMilliseconds;
                await _dbLogger.LogAsync("Debug", "Scoring computed", new { CandidateCount = candidateTuples.Count, ElapsedMs = t4 - t4Start });

                var t5Start = sw.ElapsedMilliseconds;
                var ranked = scores.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).Take(topN).ToList();
                var t5 = sw.ElapsedMilliseconds;
                await _dbLogger.LogAsync("Information", "Candidates built", new { ConversationTextLen = (normalizedText ?? string.Empty).Length, TopIds = ranked, TotalElapsedMs = sw.ElapsedMilliseconds, StepLoadEmbMs = t1 - t0, StepLoadFaqMs = t2 - t2Start, StepPrepareTuplesMs = t3 - t3Start, StepScoringMs = t4 - t4Start, StepRankMs = t5 - t5Start });

                return ranked;
            }
            catch (Exception ex)
            {
                await _dbLogger.LogAsync("Warning", "CandidateBuilder.BuildCandidatesAsync failed", new { Text = normalizedText }, ex);
                throw;
            }
        }
    }
}
