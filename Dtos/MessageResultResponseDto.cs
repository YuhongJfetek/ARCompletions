using System;

namespace ARCompletions.Dtos
{
    public class MessageResultResponseDto
    {
        public bool Success { get; set; }
        public string TraceId { get; set; } = "";
        // Mirror of MessageResult model metadata
        public string? MessageResultId { get; set; }
        public string? MessageRouteId { get; set; }
        public string? VendorId { get; set; }
        public string? ConversationId { get; set; }
        public string? MessageId { get; set; }
        public string? Source { get; set; }
        public string? Route { get; set; }
        public string? MatchedBy { get; set; }
        public double? MatchedScore { get; set; }
        public string? Payload { get; set; }
        public string? MatchedFaqId { get; set; }
        public double? Confidence { get; set; }
        public long? CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public int? ConversationLogId { get; set; }
        public int? FaqQueryLogId { get; set; }
        public SaveStateDto Saved { get; set; } = new();
    }

    public class SaveStateDto
    {
        public bool ConversationLog { get; set; }
        public bool FaqQueryLog { get; set; }
        public bool GroupState { get; set; }
        public bool FileState { get; set; }
        public bool Feedback { get; set; }
        public bool AuditLog { get; set; }
    }
}
