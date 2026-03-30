using System;
using System.Collections.Generic;

namespace ARCompletions.Dtos
{
    public class MessageAnalyzeRequestDto
    {
        public string TraceId { get; set; } = "";
        public string SourceType { get; set; } = "";     // user/group/room
        public string EventType { get; set; } = "";      // message/join/postback/file/follow
        public string MessageType { get; set; } = "";    // text/file/image/audio/video
        public string? LineGroupId { get; set; }
        public string? LineUserId { get; set; }
        public string? Text { get; set; }
        public string Language { get; set; } = "zh";
        public DateTime ReceivedAt { get; set; }
        public Dictionary<string, string>? NodeMeta { get; set; }
    }
}
