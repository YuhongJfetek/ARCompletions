using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ARCompletions.Services
{
    public interface IScoringService
    {
        Dictionary<string,double> ScoreCandidates(double[] queryVec, IEnumerable<(string FaqId, double[]? Vec, string Question, string SearchTextCache)> candidates, string normalizedText, IReadOnlyDictionary<string, ARCompletions.Domain.BotFaqItem>? faqMap = null);
        List<CandidateScoreDetail> ScoreCandidatesDetailed(double[] queryVec, IEnumerable<(string FaqId, double[]? Vec, string Question, string SearchTextCache)> candidates, string normalizedText, IReadOnlyDictionary<string, ARCompletions.Domain.BotFaqItem>? faqMap = null);
        double CosineSimilarity(double[] a, double[] b);
    }
}
