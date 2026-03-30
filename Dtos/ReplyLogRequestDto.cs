using System;

namespace ARCompletions.Dtos
{
    public class ReplyLogRequestDto
    {
        public long EventRowId { get; set; }
        public string SourceGroupId { get; set; } = string.Empty;
        public string? TargetGroupId { get; set; }
        public string? Route { get; set; }
        public string? Reason { get; set; }
        public string? MatchedFaqId { get; set; }
        public string? MatchedBy { get; set; }
        public double? MatchedScore { get; set; }
        public string? FaqCategory { get; set; }
        public string? ReplyText { get; set; }
        public string? LlmModel { get; set; }
        public string? EmbeddingModel { get; set; }
        public bool LlmEnabled { get; set; }
        public bool NeedsHumanHandoff { get; set; }
        public DateTime PushedAt { get; set; }
    }
}
