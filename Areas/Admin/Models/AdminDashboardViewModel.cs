namespace ARCompletions.Areas.Admin.Models;

public class AdminDashboardViewModel
{
    public int BotFaqItemCount { get; set; }
    public int BotConversationStateCount { get; set; }
    public int BotMessageRouteCount { get; set; }
    public int PendingEmbeddingJobs { get; set; }
    public int RecentErrorsCount { get; set; }
    public System.Collections.Generic.List<ConversationSummaryViewModel> RecentConversations { get; set; } = new();
    public System.Collections.Generic.List<string> RecentErrors { get; set; } = new();
}
