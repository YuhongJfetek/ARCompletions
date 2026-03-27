namespace ARCompletions.Domain;

public class Conversation
{
    public string Id { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public string Platform { get; set; } = default!;
    public string Channel { get; set; } = default!;
    public string ExternalUserId { get; set; } = default!;
    public string? DisplayName { get; set; }
    public long StartedAt { get; set; }
    public long? EndedAt { get; set; }
    public int MessageCount { get; set; }
    public long? LastMessageAt { get; set; }
    public string Status { get; set; } = default!;
    public long CreatedAt { get; set; }
    public long? UpdatedAt { get; set; }
}
