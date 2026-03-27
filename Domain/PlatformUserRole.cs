namespace ARCompletions.Domain;

public class PlatformUserRole
{
    public string Id { get; set; } = default!;
    public string PlatformUserId { get; set; } = default!;
    public string PlatformRoleId { get; set; } = default!;
    public long CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
