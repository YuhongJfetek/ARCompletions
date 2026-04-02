namespace ARCompletions.Domain;

public class BotStaffUser
{
    public string UserId { get; set; } = default!; // LINE userId
    public string? Name { get; set; }
    public string? Role { get; set; } // 'admin' | 'staff'
    public bool Enabled { get; set; } = true;
    public System.DateTimeOffset CreatedAt { get; set; } = System.DateTimeOffset.UtcNow;
    public System.DateTimeOffset? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
