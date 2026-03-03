namespace ARCompletions.Dto
{
    public class AnalysisDetailDto
    {
        public string Id { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string CompletionId { get; set; } = string.Empty;
        public double Similarity { get; set; }
        public int RiskLevel { get; set; }
        public string Flags { get; set; } = string.Empty;
    }
}
