using System;
using System.ComponentModel.DataAnnotations;

namespace ARCompletions.Data
{
    public class LineEventLog
    {
        [Key]
        public long Id { get; set; }
        public string LineUserId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string? MessageType { get; set; }
        public string? Text { get; set; }
        public string RawJson { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
