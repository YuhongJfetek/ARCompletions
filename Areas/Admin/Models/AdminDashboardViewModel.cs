namespace ARCompletions.Areas.Admin.Models;

public class AdminDashboardViewModel
{
    public int VendorCount { get; set; }
    public int ActiveVendorCount { get; set; }
    public int ConversationCount { get; set; }
    public int FaqCount { get; set; }
    public int FaqCandidateCount { get; set; }
    public int EmbeddingJobCount { get; set; }
    public int AiFaqAnalysisJobCount { get; set; }
}
