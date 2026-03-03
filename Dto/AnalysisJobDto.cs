using System.Collections.Generic;

namespace ARCompletions.Dto
{
    public class AnalysisJobDto
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Progress { get; set; }
        public long CreatedAt { get; set; }
        public long? StartedAt { get; set; }
        public long? FinishedAt { get; set; }
        public string ResultSummary { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public IEnumerable<AnalysisDetailDto>? Details { get; set; }
    }
}
