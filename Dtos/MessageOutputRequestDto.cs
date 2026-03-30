using System;
using System.Collections.Generic;

namespace ARCompletions.Dtos
{
    // Minimal request DTO for Output endpoint — only include fields needed by backend
    public class MessageOutputRequestDto
    {
        public string TraceId { get; set; } = "";
        public string? SourceType { get; set; }
        public string? MessageType { get; set; }
        public string? LineGroupId { get; set; }
        public string? LineUserId { get; set; }
        public string? Text { get; set; }
        public DateTime ReceivedAt { get; set; }
        public Dictionary<string, string>? NodeMeta { get; set; }
        public List<AttachmentDto>? Attachments { get; set; }
    }
}
