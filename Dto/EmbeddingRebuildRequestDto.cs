namespace ARCompletions.Dto
{
    public class EmbeddingRebuildRequestDto
    {
        public string? Provider { get; set; } // legacy_hash64 / openai
        public string? Model { get; set; }    // 對應模型名稱
        public string Scope { get; set; } = "all"; // all / single
        public string? FaqId { get; set; }
    }
}
