using System;
using System.ComponentModel.DataAnnotations;

namespace ARCompletions.Data
{
    public class LineEvent
    {
        [Key]
        public string Id { get; set; }

        // 原始事件 id 或自訂唯一識別
        public string EventId { get; set; }

        // 可能存在的 userId
        public string UserId { get; set; }

        // 事件類型 (message, follow, etc.)
        public string EventType { get; set; }

        // 儲存原始 JSON
        public string RawEvent { get; set; }

        // 收到時間 (Unix seconds)
        public long ReceivedAt { get; set; }

        // 是否已被後續 Processor 處理
        public bool Processed { get; set; }

        public LineEvent()
        {
            Id = Guid.NewGuid().ToString();
            EventId = Guid.NewGuid().ToString();
            Processed = false;
            ReceivedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
