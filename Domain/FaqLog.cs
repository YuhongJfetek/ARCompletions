namespace ARCompletions.Domain;

public class FaqLog
{
    public string Id { get; set; } = default!;
    public string VendorId { get; set; } = default!;
    public string FaqId { get; set; } = default!;
    public string ActionType { get; set; } = default!;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string OperatedByType { get; set; } = default!;
    public string OperatedById { get; set; } = default!;
    public long OperatedAt { get; set; }
}
