using System;

namespace ARCompletions.Dtos
{
    public class MessageResultResponseDto
    {
        public bool Success { get; set; }
        public string TraceId { get; set; } = "";
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
