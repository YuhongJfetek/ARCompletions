using System;
using System.ComponentModel.DataAnnotations;

namespace ARCompletions.Data
{
    public class UnmatchedQuery
    {
        [Key]
        public string Id { get; set; }

        // 原始查詢文字
        public string Query { get; set; }

        // 來源 (e.g. "line", "web", "api")
        public string Source { get; set; }

        // 建立時間 (Unix seconds)
        public long CreatedAt { get; set; }

        public UnmatchedQuery()
        {
            Id = Guid.NewGuid().ToString();
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
