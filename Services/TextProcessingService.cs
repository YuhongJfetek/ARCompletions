using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ARCompletions.Services
{
    public class TextProcessingService : ITextProcessingService
    {
        public string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var s = text.Trim();
            s = s.Replace('，', ',').Replace('。', '.').Replace('？', '?').Replace('！', '!').Replace('：', ':').Replace('；', ';');
            s = Regex.Replace(s, @"\u00A0|\s+", " ");
            return s.ToLowerInvariant();
        }

        public string[] Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
            // Use Unicode-aware tokenization: letters, numbers, and underscore
            return Regex.Matches(text, @"[\p{L}\p{N}_]+")
                .Select(m => m.Value)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToArray();
        }

        public bool IsShortChit(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var tokens = Tokenize(text);
            if (tokens.Length == 0) return true;
            if (tokens.Length < 2)
            {
                var chit = new HashSet<string> { "hi", "hello", "你好", "謝謝", "thanks", "ok", "好的", "嗨" };
                if (chit.Contains(tokens[0])) return true;
            }
            return false;
        }

        public bool IsNonText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            // Consider non-text only if there are no Unicode letters
            return !Regex.IsMatch(text, @"\p{L}");
        }

        public bool IsComposite(string normalizedText, string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(normalizedText)) return false;
            if (normalizedText.Contains("、") || normalizedText.Contains(",") || normalizedText.Contains(" or ") || normalizedText.Contains(" and ") || normalizedText.Contains(" 或 ") || normalizedText.Contains(" 和 ") || normalizedText.Contains(" 以及 "))
                return true;

            var qwords = new[] { "what", "how", "why", "where", "when", "which", "who", "為何", "什麼", "哪裡", "如何", "多少", "幾點", "怎麼", "怎樣" };
            var qcount = qwords.Count(q => normalizedText.Contains(q));
            if (qcount >= 2) return true;

            if (tokens != null && tokens.Length >= 4)
            {
                var uniq = tokens.Distinct().Count();
                if (uniq >= 3 && normalizedText.Length > 40) return true;
            }

            return false;
        }

        public double TokenOverlapScore(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0.0;
            var ta = Regex.Matches(a.ToLowerInvariant(), @"[\p{L}\p{N}_]+").Select(m => m.Value).Distinct();
            var tb = Regex.Matches(b.ToLowerInvariant(), @"[\p{L}\p{N}_]+").Select(m => m.Value).Distinct();
            var setA = new HashSet<string>(ta);
            var setB = new HashSet<string>(tb);
            if (setA.Count == 0 || setB.Count == 0) return 0.0;
            var inter = setA.Intersect(setB).Count();
            var uni = setA.Union(setB).Count();
            return uni == 0 ? 0.0 : (double)inter / uni;
        }
    }
}
