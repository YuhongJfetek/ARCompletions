using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ARCompletions.Services
{
    public class PrefilterService : IPrefilterService
    {
        public PrefilterResult EvaluatePrefilter(string normalizedText, string[] tokens)
        {
            var res = new PrefilterResult { ShortCircuit = false, IsStaffTriggered = false, Reason = null };

            // Consider text present if there is at least one Unicode letter (supports CJK)
            var hasLetters = !string.IsNullOrWhiteSpace(normalizedText) && Regex.IsMatch(normalizedText, "\\p{L}");
            var isNonText = !hasLetters;

            var chit = new HashSet<string> { "hi", "hello", "你好", "謝謝", "thanks", "ok", "好的", "嗨" };
            var isShortChit = tokens != null && tokens.Length > 0 && tokens.Length < 2 && chit.Contains(tokens[0]);
            var isStaffTrigger = (!string.IsNullOrWhiteSpace(normalizedText) && (normalizedText.StartsWith("/staff", StringComparison.OrdinalIgnoreCase) || normalizedText.Contains("@staff", StringComparison.OrdinalIgnoreCase)));

            if (isNonText || isShortChit || isStaffTrigger)
            {
                res.ShortCircuit = true;
                res.IsStaffTriggered = isStaffTrigger;
                res.Reason = isNonText ? "non_text" : isShortChit ? "short_chat" : "staff_triggered";
            }

            return res;
        }
    }
}
