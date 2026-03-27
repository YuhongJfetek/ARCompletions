namespace ARCompletions.Domain;

public class Vendor
{
    public string Id { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? ContactName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? LineChannelId { get; set; }
    public string? LineChannelSecret { get; set; }
    public string? LineAccessToken { get; set; }
    public string? OpenAiApiKey { get; set; }
    public string? ChatModel { get; set; }
    public string? EmbeddingModel { get; set; }
    public string? PromptVersion { get; set; }
    public double? AnswerThreshold { get; set; }
    public bool IsActive { get; set; }
    public string? Remark { get; set; }
    public long CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public long? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
