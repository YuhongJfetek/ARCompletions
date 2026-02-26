using System;
using System.ComponentModel.DataAnnotations;

namespace ARCompletions.Data
{
    public class AnalysisJob
    {
        [Key]
        public string Id { get; set; }
        public string Type { get; set; }
        public string Params { get; set; }
        public string Status { get; set; }
        public int Progress { get; set; }
        public string ResultSummary { get; set; }
        public long CreatedAt { get; set; }
        public long? StartedAt { get; set; }
        public long? FinishedAt { get; set; }
        public string Error { get; set; }

        public AnalysisJob()
        {
            Id = Guid.NewGuid().ToString();
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Status = "Queued";
            Progress = 0;
        }
    }
}
