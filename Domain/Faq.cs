namespace ARCompletions.Domain;

public class Faq
{
    public string Id { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public string CategoryId { get; set; } = default!;
    public string Question { get; set; } = default!;
    public string Answer { get; set; } = default!;
    public string? Tags { get; set; }
    public string Status { get; set; } = default!;
    public int Priority { get; set; }
    public string? SourceCandidateId { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public string? CreatedByType { get; set; }
    public string? CreatedById { get; set; }
    public string? UpdatedByType { get; set; }
    public string? UpdatedById { get; set; }
    public long CreatedAt { get; set; }
    public long? UpdatedAt { get; set; }
}
