using System;

namespace ARCompletions.Dtos
{
    public class MessageRouteCreateDto
    {
        public string TraceId { get; set; } = Guid.NewGuid().ToString("N");
        public string? VendorId { get; set; }
        public string? InputLogId { get; set; }
        public string? ConversationId { get; set; }
        public string? Route { get; set; }
        public string? Reason { get; set; }
        public string? MatchedFaqId { get; set; }
        public double? MatchedScore { get; set; }
        public string? MatchedBy { get; set; }
        public string? FaqCategory { get; set; }
        public bool LlmEnabled { get; set; }
        public bool NeedsHandoff { get; set; }
        public string? ReplyText { get; set; }
    }

    public class MessageRouteResponseDto
    {
        public bool Success { get; set; }
        public string TraceId { get; set; } = "";
        public bool Saved { get; set; }
        public string? MessageRouteId { get; set; }
    }
}
