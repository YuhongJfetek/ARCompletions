using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ARCompletions.Data;
using ARCompletions.Domain;

namespace ARCompletions.Services
{
    public class ScoringService : IScoringService
    {
        private readonly ITextProcessingService _textProcessing;
        private readonly IQueryHintsService _queryHints;
        private readonly ARCompletionsContext _db;
        public ScoringService(ITextProcessingService textProcessing, IQueryHintsService queryHints, ARCompletionsContext db)
        {
            _textProcessing = textProcessing;
            _queryHints = queryHints;
            _db = db;
        }

        // CandidateScoreDetail moved to Services/CandidateScoreDetail.cs

        private void WriteAppLogSync(string level, string message, object? props = null)
        {
            try
            {
                var log = new AppLog
                {
                    Id = Guid.NewGuid().ToString(),
                    TimeStamp = DateTime.UtcNow,
                    Level = level,
                    Message = message,
                    MessageTemplate = message,
                    Properties = props == null ? null : System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(props))
                };
                _db.AppLogs.Add(log);
                _db.SaveChanges();
            }
            catch
            {
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

        public Dictionary<string,double> ScoreCandidates(double[] queryVec, IEnumerable<(string FaqId, double[]? Vec, string Question, string SearchTextCache)> candidates, string normalizedText, IReadOnlyDictionary<string, ARCompletions.Domain.BotFaqItem>? faqMap = null)
        {
            var scores = new Dictionary<string,double>();
            var candList = candidates.ToList();
            WriteAppLogSync("Debug", "Scoring candidates", new { Count = candList.Count, Len = (normalizedText ?? string.Empty).Length });
            foreach (var c in candList)
            {
                // Follow hash-based embedding scoring: final = cosine + tokenOverlap*0.025 (capped) + categoryBonus; cap at 0.99
                var questionText = (c.Question ?? string.Empty);
                var searchCache = (c.SearchTextCache ?? string.Empty);
                var cosine = 0.0;
                if (queryVec != null && c.Vec != null) cosine = CosineSimilarity(queryVec, c.Vec);
                var tokenOverlap = _textProcessing.TokenOverlapScore(normalizedText ?? string.Empty, searchCache);
                var tokenScore = Math.Min(tokenOverlap * 0.025, 0.15);

                // category bonus: if normalized text mentions candidate's category key or category, give small boost
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
                        // fallback heuristic: if question text tokens overlap query tokens, small boost
                        var norm = (normalizedText ?? string.Empty);
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
            }
            if (scores.Count > 0)
            {
                var best = scores.OrderByDescending(kv => kv.Value).First();
                WriteAppLogSync("Debug", "Scoring best", new { FaqId = best.Key, Score = best.Value });
            }
            return scores;
        }

        public List<CandidateScoreDetail> ScoreCandidatesDetailed(double[] queryVec, IEnumerable<(string FaqId, double[]? Vec, string Question, string SearchTextCache)> candidates, string normalizedText, IReadOnlyDictionary<string, ARCompletions.Domain.BotFaqItem>? faqMap = null)
        {
            var details = new List<CandidateScoreDetail>();
            var candList = candidates.ToList();
            WriteAppLogSync("Debug", "Scoring candidates (detailed)", new { Count = candList.Count, Len = (normalizedText ?? string.Empty).Length });
            foreach (var c in candList)
            {
                var qNorm = (c.Question ?? string.Empty);
                var questionSimilarity = _textProcessing.TokenOverlapScore(normalizedText ?? string.Empty, qNorm);
                var searchSimilarity = 0.0;
                if (queryVec != null && c.Vec != null) searchSimilarity = CosineSimilarity(queryVec, c.Vec);
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

                details.Add(new CandidateScoreDetail
                {
                    FaqId = c.FaqId,
                    Cosine = searchSimilarity,
                    QuestionSimilarity = questionSimilarity,
                    SearchSimilarity = searchSimilarity,
                    KeywordScore = keywordScore,
                    Overlap = tokenOverlap,
                    FinalScore = finalScore
                });
            }
            if (details.Count > 0)
            {
                var best = details.OrderByDescending(d => d.FinalScore).First();
                WriteAppLogSync("Debug", "Scoring best (detailed)", new { FaqId = best.FaqId, Score = best.FinalScore });
            }
            return details;
        }
    }
}
