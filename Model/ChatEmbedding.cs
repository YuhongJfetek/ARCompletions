using System;
using System.ComponentModel.DataAnnotations;

namespace ARCompletions.Data
{
    public class ChatEmbedding
    {
        [Key]
        public string Id { get; set; }

        public string ChatMessageId { get; set; }

        // store embedding as JSON text for SQLite fallback; Postgres migration will create 'vector' column
        public string EmbeddingJson { get; set; }

        public long CreatedAt { get; set; }

        public ChatEmbedding()
        {
            Id = Guid.NewGuid().ToString();
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
