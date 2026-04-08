using System;

namespace ARCompletions.Services
{
    public interface ITextProcessingService
    {
        string Normalize(string text);
        string[] Tokenize(string text);
        bool IsShortChit(string text);
        bool IsNonText(string text);
        bool IsComposite(string normalizedText, string[] tokens);
        double TokenOverlapScore(string a, string b);
    }
}
