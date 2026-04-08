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
        private readonly ARCompletionsContext _db;
        public ScoringService(ITextProcessingService textProcessing, ARCompletionsContext db)
        {
            _textProcessing = textProcessing;
            _db = db;
        }

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

        public Dictionary<string,double> ScoreCandidates(double[] queryVec, IEnumerable<(string FaqId, double[]? Vec, string Question, string SearchTextCache)> candidates, string normalizedText)
        {
            var scores = new Dictionary<string,double>();
            var candList = candidates.ToList();
            WriteAppLogSync("Debug", "Scoring candidates", new { Count = candList.Count, Len = (normalizedText ?? string.Empty).Length });
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
                WriteAppLogSync("Debug", "Scoring best", new { FaqId = best.Key, Score = best.Value });
            }
            return scores;
        }
    }
}
