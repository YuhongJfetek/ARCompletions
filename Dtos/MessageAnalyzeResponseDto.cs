using System;
using System.Collections.Generic;

namespace ARCompletions.Dtos
{
    public class MessageAnalyzeResponseDto
    {
        public bool Success { get; set; }
        public string TraceId { get; set; } = "";
        public string Route { get; set; } = "";              // faq_match / low_confidence / fallback / summary_trigger ...
        public string? MatchedFaqId { get; set; }
        public string? Answer { get; set; }
        public decimal? BestScore { get; set; }
        public string? ReasonCode { get; set; }
        public List<FaqCandidateDto> TopCandidates { get; set; } = new();
        public bool FeedbackEnabled { get; set; }
        public string? PersonaApplied { get; set; }
        public string? ResponseTone { get; set; }
        public string NodeAction { get; set; } = "reply_text";   // reply_text / fallback / handoff / show_summary_status
        public bool FallbackSuggested { get; set; }
        public List<RecommendedJobDto> Jobs { get; set; } = new();
        public AnalyzeDebugDto? Debug { get; set; }
    }

    public class FaqCandidateDto
    {
        public string FaqId { get; set; } = "";
        public decimal Score { get; set; }
    }

    public class RecommendedJobDto
    {
        public string JobType { get; set; } = "";   // summary / archive / embedding_rebuild
        public string? JobCode { get; set; }
        public string? Note { get; set; }
    }

    public class AnalyzeDebugDto
    {
        public string? NormalizedText { get; set; }
        public string? ActiveEmbeddingVersion { get; set; }
    }
}
