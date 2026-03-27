namespace ARCompletions.Domain;

public class FaqCategory
{
    public string Id { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public int Sort { get; set; }
    public bool IsActive { get; set; }
    public long CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public long? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
