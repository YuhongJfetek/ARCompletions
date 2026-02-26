using System;
using System.ComponentModel.DataAnnotations;

namespace ARCompletions.Data
{
    public class ChatMessage
    {
        [Key]
        public string Id { get; set; }

        // 對話所屬的 user
        public string UserId { get; set; }

        // 訊息來源 (user / ai / system)
        public string Role { get; set; }

        // 訊息內容
        public string Content { get; set; }

        // 可選的 ai 回覆分數或 metadata
        public string Meta { get; set; }

        // 建立時間 (Unix seconds)
        public long CreatedAt { get; set; }

        public ChatMessage()
        {
            Id = Guid.NewGuid().ToString();
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
