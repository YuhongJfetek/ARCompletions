namespace ARCompletions.Domain;

public class VendorAccount
{
    public string Id { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public string LoginEmail { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string AccountName { get; set; } = default!;
    public string? Phone { get; set; }
    public bool IsActive { get; set; }
    public long? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public long CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public long? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
