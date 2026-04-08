using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ARCompletions.Services
{
    public interface IScoringService
    {
        Dictionary<string,double> ScoreCandidates(double[] queryVec, IEnumerable<(string FaqId, double[]? Vec, string Question, string SearchTextCache)> candidates, string normalizedText);
        double CosineSimilarity(double[] a, double[] b);
    }
}
