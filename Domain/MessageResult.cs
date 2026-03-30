using System;

namespace ARCompletions.Domain
{
    public class MessageResult
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string? VendorId { get; set; }
        public string? ConversationId { get; set; }
        public string? MessageId { get; set; }
        public string? Source { get; set; }
        public string? Route { get; set; }
        public string? MessageRouteId { get; set; }
        public string? MatchedBy { get; set; }
        public double? MatchedScore { get; set; }
        public string? Payload { get; set; }
        public string? MatchedFaqId { get; set; }
        public double? Confidence { get; set; }
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public string? CreatedBy { get; set; }
    }
}
