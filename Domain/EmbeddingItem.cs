namespace ARCompletions.Domain;

public class EmbeddingItem
{
    public string Id { get; set; } = default!;
    public string EmbeddingJobId { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public string FaqId { get; set; } = default!;
    public string ChunkText { get; set; } = default!;
    public string EmbeddingJson { get; set; } = default!;
    // 如果使用 PostgreSQL + pgvector，可考慮改用 float[] 與 pgvector extension
    // 目前儲存為 float[]（對應到 Postgres 的 real[]），也保留原始 JSON
    public float[]? EmbeddingVector { get; set; }
    public int TokenCount { get; set; }
    public string Status { get; set; } = default!;
    public string? ErrorMessage { get; set; }
    public long CreatedAt { get; set; }
}
