namespace ARCompletions.Domain;

public class BotAuditLog
{
    public long AuditId { get; set; }
    public string? ActionType { get; set; }
    public string? TargetTable { get; set; }
    public string? TargetId { get; set; }
    public string? ChangedBy { get; set; }
    public System.DateTimeOffset ChangedAt { get; set; } = System.DateTimeOffset.UtcNow;
    public string? OldValue { get; set; } // JSON
    public string? NewValue { get; set; } // JSON
    public string? Note { get; set; }
}
