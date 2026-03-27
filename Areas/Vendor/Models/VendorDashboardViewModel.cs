namespace ARCompletions.Areas.Vendor.Models;

public class VendorDashboardViewModel
{
    public string VendorId { get; set; } = string.Empty;
    public int ConversationCount { get; set; }
    public int FaqCount { get; set; }
    public int FaqCandidateCount { get; set; }
    public int EmbeddingJobCount { get; set; }
}
