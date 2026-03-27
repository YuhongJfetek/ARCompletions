namespace ARCompletions.Domain;

public class PlatformUserVendorScope
{
    public string Id { get; set; } = default!;
    public string PlatformUserId { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public long CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}
