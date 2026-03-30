using System;
using System.Text.Json.Nodes;

namespace ARCompletions.Dtos
{
    public class IncomingEventRequestDto
    {
        public string SourceGroupId { get; set; } = string.Empty;
        public string? SourceGroupName { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string? MessageText { get; set; }
        public DateTime ReceivedAt { get; set; }
        public JsonObject? RawEvent { get; set; }
    }
}
