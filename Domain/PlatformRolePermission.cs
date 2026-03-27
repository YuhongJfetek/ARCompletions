namespace ARCompletions.Domain;

public class PlatformRolePermission
{
    public string Id { get; set; } = default!;
    public string PlatformRoleId { get; set; } = default!;
    public string PlatformPermissionId { get; set; } = default!;
    public long CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
