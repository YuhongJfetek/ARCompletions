using System;

namespace ARCompletions.Services
{
    public class PrefilterResult
    {
        public bool ShortCircuit { get; set; }
        public bool IsStaffTriggered { get; set; }
        public string? Reason { get; set; }
    }

    public interface IPrefilterService
    {
        PrefilterResult EvaluatePrefilter(string normalizedText, string[] tokens);
    }
}
