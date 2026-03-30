namespace ARCompletions.Domain;

public class VendorStaffUser
{
    public string Id { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public string LineUserId { get; set; } = default!; // LINE userId (Uxxxxxxx)
    public string? Name { get; set; }
    public string? Role { get; set; }
    public bool IsActive { get; set; } = true;
    public long CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public long? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
