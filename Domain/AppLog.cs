using System;

namespace ARCompletions.Domain
{
    public class AppLog
    {
        public string Id { get; set; } = string.Empty;
        public DateTime TimeStamp { get; set; }
        public string? Level { get; set; }
        public string? Message { get; set; }
        public string? MessageTemplate { get; set; }
        public string? Exception { get; set; }
        public System.Text.Json.JsonDocument? Properties { get; set; }
        public string? LogEvent { get; set; }
    }
}
