namespace ARCompletions.Domain;

public class BotSystemPrompt
{
    public string PromptKey { get; set; } = default!; // 'customerAssistant', 'staffAssistantFallback'
    public string PromptText { get; set; } = default!;
    public System.DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
