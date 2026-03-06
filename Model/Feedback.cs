using System;
using System.ComponentModel.DataAnnotations;

namespace ARCompletions.Data
{
    public class Feedback
    {
        [Key]
        public string Id { get; set; }

        // 可選的使用者 ID
        public string? UserId { get; set; }

        // 評分（例如 1-5）
        public int Rating { get; set; }

        // 可選評論
        public string? Comment { get; set; }

        // 建立時間 (Unix seconds)
        public long CreatedAt { get; set; }

        public Feedback()
        {
            Id = Guid.NewGuid().ToString();
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
