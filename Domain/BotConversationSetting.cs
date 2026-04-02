namespace ARCompletions.Domain;

public class BotConversationSetting
{
    public string SourceType { get; set; } = default!; // 'group' | 'room'
    public string ConversationId { get; set; } = default!; // groupId / roomId
    public bool Enabled { get; set; } = true;
    public System.DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
