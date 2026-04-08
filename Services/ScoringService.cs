using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace ARCompletions.Services
{
    public class ScoringService : IScoringService
    {
        private readonly ITextProcessingService _textProcessing;
        private readonly ILogger<ScoringService> _logger;
        public ScoringService(ITextProcessingService textProcessing, ILogger<ScoringService> logger)
        {
            _textProcessing = textProcessing;
            _logger = logger;
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

        public Dictionary<string,double> ScoreCandidates(double[] queryVec, IEnumerable<(string FaqId, double[]? Vec, string Question, string SearchTextCache)> candidates, string normalizedText)
        {
            var scores = new Dictionary<string,double>();
            var candList = candidates.ToList();
            _logger?.LogDebug("Scoring {Count} candidates for text length {Len}", candList.Count, (normalizedText ?? string.Empty).Length);
            foreach (var c in candList)
            {
                var qNorm = (c.Question ?? string.Empty);
                var questionSimilarity = _textProcessing.TokenOverlapScore(normalizedText ?? string.Empty, qNorm);
                var searchSimilarity = 0.0;
                if (queryVec != null && c.Vec != null) searchSimilarity = CosineSimilarity(queryVec, c.Vec);
                var keywordScore = _textProcessing.TokenOverlapScore(normalizedText ?? string.Empty, (c.SearchTextCache ?? string.Empty));
                var overlap = _textProcessing.TokenOverlapScore(normalizedText ?? string.Empty, qNorm);
                double score = questionSimilarity * 0.65 + searchSimilarity * 0.20 + keywordScore * 0.08 + overlap * 0.05;
                if (score < 0) score = 0;
                if (score > 0.99) score = 0.99;
                scores[c.FaqId] = score;
            }
            if (scores.Count > 0)
            {
                var best = scores.OrderByDescending(kv => kv.Value).First();
                _logger?.LogDebug("Scoring best: FaqId={FaqId} Score={Score}", best.Key, best.Value);
            }
            return scores;
        }
    }
}
