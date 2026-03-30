using System;

namespace ARCompletions.Domain
{
    // Maps to existing LineEventLogs table (Id BIGSERIAL)
    public class LineEventLog
    {
        public long Id { get; set; }
        public string? LineUserId { get; set; }
        public string? EventType { get; set; }
        public string? MessageType { get; set; }
        public string? Text { get; set; }
        public string RawJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        // New fields for processing/linkage
        public string? MessageResultId { get; set; }
        public string? ProcessingStatus { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
