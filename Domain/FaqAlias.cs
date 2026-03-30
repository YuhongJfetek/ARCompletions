namespace ARCompletions.Domain;

public class FaqAlias
{
    public string Id { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public string Term { get; set; } = default!;
    public string? Synonyms { get; set; } // JSON array
    public string Mode { get; set; } = "direct"; // 'direct' | 'disambiguation'
    public string FaqIds { get; set; } = default!; // JSON array of Faq Ids
    public bool IsActive { get; set; } = true;
    public long CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public long? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
