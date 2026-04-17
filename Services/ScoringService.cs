using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ARCompletions.Data;
using ARCompletions.Domain;

namespace ARCompletions.Services
{
    public class ScoringService : IScoringService
    {
        private readonly ITextProcessingService _textProcessing;
        private readonly IQueryHintsService _queryHints;
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> _dbFactory;
        private readonly IBufferedAppLogger _bufferedLogger;
        public ScoringService(ITextProcessingService textProcessing, IQueryHintsService queryHints, Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> dbFactory, IBufferedAppLogger bufferedLogger)
        {
            _textProcessing = textProcessing;
            _queryHints = queryHints;
            _dbFactory = dbFactory;
            _bufferedLogger = bufferedLogger ?? throw new ArgumentNullException(nameof(bufferedLogger));
        }
        // CandidateScoreDetail moved to Services/CandidateScoreDetail.cs

        private void WriteAppLogSync(string level, string message, object? props = null)
        {
            try
            {
                // Enqueue log for background persistence to avoid blocking the scoring path.
                // Fire-and-forget: buffered logger will create a scope and persist.
                _bufferedLogger?.EnqueueLogAsync(level, message, props);
            }
            catch
            {
                // swallow to avoid affecting scoring flow
            }
        }

        public double CosineSimilarity(double[] a, double[] b)
        {
            if (a == null || b == null) return 0.0;
            var len = Math.Min(a.Length, b.Length);
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < len; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }
            if (na == 0 || nb == 0) return 0.0;
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }

        // SIMD-accelerated cosine similarity using System.Numerics.Vector
        public static double CosineSimilaritySimd(double[] a, double[] b)
        {
            if (a == null || b == null) return 0.0;
            int len = Math.Min(a.Length, b.Length);
            int vectorSize = Vector<double>.Count;

            int i = 0;
            var vDot = Vector<double>.Zero;
            var vA2 = Vector<double>.Zero;
            var vB2 = Vector<double>.Zero;

            for (; i <= len - vectorSize; i += vectorSize)
            {
                var va = new Vector<double>(a, i);
                var vb = new Vector<double>(b, i);
                vDot += va * vb;
                vA2 += va * va;
                vB2 += vb * vb;
            }

            double dot = 0, na = 0, nb = 0;
            for (int j = 0; j < vectorSize; j++)
            {
                dot += vDot[j];
                na += vA2[j];
                nb += vB2[j];
            }

            for (; i < len; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }

            if (na == 0 || nb == 0) return 0.0;
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }

        public Dictionary<string,double> ScoreCandidates(double[] queryVec, IEnumerable<(string FaqId, double[]? Vec, string Question, string SearchTextCache)> candidates, string normalizedText, IReadOnlyDictionary<string, ARCompletions.Domain.BotFaqItem>? faqMap = null)
        {
            var candList = candidates.ToList();
            WriteAppLogSync("Debug", "Scoring candidates", new { Count = candList.Count, Len = (normalizedText ?? string.Empty).Length });

            var scores = new ConcurrentDictionary<string, double>();

            bool useSimd = queryVec != null;

            Parallel.ForEach(candList, (c) =>
            {
                double cosine = 0.0;
                if (useSimd && c.Vec != null)
                {
                    cosine = CosineSimilaritySimd(queryVec, c.Vec);
                }
                else if (queryVec != null && c.Vec != null)
                {
                    cosine = CosineSimilarity(queryVec, c.Vec);
                }

                var searchCache = (c.SearchTextCache ?? string.Empty);
                var tokenOverlap = _textProcessing.TokenOverlapScore(normalizedText ?? string.Empty, searchCache);
                var tokenScore = Math.Min(tokenOverlap * 0.025, 0.15);

                double categoryBonus = 0.0;
                try
                {
                    if (faqMap != null && faqMap.TryGetValue(c.FaqId, out var faq))
                    {
                        var preferredCategories = _queryHints.DetectPreferredCategoryKeys(normalizedText ?? string.Empty);
                        if (preferredCategories != null && faq?.CategoryKey != null && preferredCategories.Contains(faq.CategoryKey))
                        {
                            categoryBonus = 0.08;
                        }
                    }
                    else
                    {
                        var norm = (normalizedText ?? string.Empty);
                        var questionText = (c.Question ?? string.Empty);
                        if (!string.IsNullOrWhiteSpace(questionText))
                        {
                            var qTokens = questionText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            foreach (var t in qTokens)
                            {
                                if (norm.Contains(t)) { categoryBonus = 0.08; break; }
                            }
                        }
                    }
                }
                catch { categoryBonus = 0.0; }

                var finalScore = cosine + tokenScore + categoryBonus;
                if (finalScore < 0) finalScore = 0;
                if (finalScore > 0.99) finalScore = 0.99;

                scores[c.FaqId] = finalScore;
            });

            if (scores.Count > 0)
            {
                var best = scores.OrderByDescending(kv => kv.Value).First();
                WriteAppLogSync("Debug", "Scoring best", new { FaqId = best.Key, Score = best.Value });
            }

            return scores.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public List<CandidateScoreDetail> ScoreCandidatesDetailed(double[] queryVec, IEnumerable<(string FaqId, double[]? Vec, string Question, string SearchTextCache)> candidates, string normalizedText, IReadOnlyDictionary<string, ARCompletions.Domain.BotFaqItem>? faqMap = null)
        {
            var candList = candidates.ToList();
            WriteAppLogSync("Debug", "Scoring candidates (detailed)", new { Count = candList.Count, Len = (normalizedText ?? string.Empty).Length });

            var bag = new ConcurrentBag<CandidateScoreDetail>();
            bool useSimd = queryVec != null;

            Parallel.ForEach(candList, (c) =>
            {
                var qNorm = (c.Question ?? string.Empty);
                var questionSimilarity = _textProcessing.TokenOverlapScore(normalizedText ?? string.Empty, qNorm);
                double searchSimilarity = 0.0;
                if (useSimd && c.Vec != null)
                    searchSimilarity = CosineSimilaritySimd(queryVec, c.Vec);
                else if (queryVec != null && c.Vec != null)
                    searchSimilarity = CosineSimilarity(queryVec, c.Vec);

                var keywordScore = _textProcessing.TokenOverlapScore(normalizedText ?? string.Empty, (c.SearchTextCache ?? string.Empty));
                var tokenOverlap = _textProcessing.TokenOverlapScore(normalizedText ?? string.Empty, (c.SearchTextCache ?? string.Empty));
                var tokenScore = Math.Min(tokenOverlap * 0.025, 0.15);
                double categoryBonus = 0.0;
                try
                {
                    if (faqMap != null && faqMap.TryGetValue(c.FaqId, out var faq))
                    {
                        var preferredCategories = _queryHints.DetectPreferredCategoryKeys(normalizedText ?? string.Empty);
                        if (preferredCategories != null && faq?.CategoryKey != null && preferredCategories.Contains(faq.CategoryKey))
                        {
                            categoryBonus = 0.08;
                        }
                    }
                    else
                    {
                        var norm = (normalizedText ?? string.Empty);
                        if (!string.IsNullOrWhiteSpace(qNorm))
                        {
                            var qTokens = qNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            foreach (var t in qTokens)
                            {
                                if (norm.Contains(t)) { categoryBonus = 0.08; break; }
                            }
                        }
                    }
                }
                catch { categoryBonus = 0.0; }

                var finalScore = searchSimilarity + tokenScore + categoryBonus;
                if (finalScore < 0) finalScore = 0;
                if (finalScore > 0.99) finalScore = 0.99;

                bag.Add(new CandidateScoreDetail
                {
                    FaqId = c.FaqId,
                    Cosine = searchSimilarity,
                    QuestionSimilarity = questionSimilarity,
                    SearchSimilarity = searchSimilarity,
                    KeywordScore = keywordScore,
                    Overlap = tokenOverlap,
                    FinalScore = finalScore
                });
            });

            var details = bag.ToList();
            if (details.Count > 0)
            {
                var best = details.OrderByDescending(d => d.FinalScore).First();
                WriteAppLogSync("Debug", "Scoring best (detailed)", new { FaqId = best.FaqId, Score = best.FinalScore });
            }
            return details;
        }
    }
}
