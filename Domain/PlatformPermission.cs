namespace ARCompletions.Domain;

public class PlatformPermission
{
    public string Id { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? GroupName { get; set; }
    public string? Description { get; set; }
    public long CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
