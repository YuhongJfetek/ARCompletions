namespace ARCompletions.Domain;

public class EmbeddingSetting
{
    public string Id { get; set; } = default!; // GUID
    public string VendorId { get; set; } = default!;
    public string? ActiveVectorVersion { get; set; }
    public long UpdatedAt { get; set; }
}
