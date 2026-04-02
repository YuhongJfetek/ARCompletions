namespace ARCompletions.Domain;

public class BotFaqEmbedding
{
    public System.Guid EmbeddingId { get; set; }
    public string FaqId { get; set; } = default!;
    public string? Question { get; set; }
    public string? SearchText { get; set; }
    public string? CategoryKey { get; set; }
    public string EmbeddingProvider { get; set; } = "local_hash";
    public string EmbeddingModel { get; set; } = "legacy_hash64";
    public int VectorDim { get; set; } = 64;
    public double[] Embedding { get; set; } = System.Array.Empty<double>();
    public bool IsActive { get; set; } = true;
    public System.DateTimeOffset CreatedAt { get; set; } = System.DateTimeOffset.UtcNow;
    public System.DateTimeOffset? RebuiltAt { get; set; }
}
