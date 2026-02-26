using System;
using System.ComponentModel.DataAnnotations;

namespace ARCompletions.Data
{
    public class AnalysisDetail
    {
        [Key]
        public string Id { get; set; }
        public string JobId { get; set; }
        public string CompletionId { get; set; }
        public double Similarity { get; set; }
        public int RiskLevel { get; set; }
        public string Flags { get; set; }

        public AnalysisDetail()
        {
            Id = Guid.NewGuid().ToString();
        }
    }
}
