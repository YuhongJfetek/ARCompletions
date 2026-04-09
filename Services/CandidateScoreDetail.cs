namespace ARCompletions.Services
{
    public class CandidateScoreDetail
    {
        public string FaqId { get; set; } = string.Empty;
        public double Cosine { get; set; }
        public double QuestionSimilarity { get; set; }
        public double SearchSimilarity { get; set; }
        public double KeywordScore { get; set; }
        public double Overlap { get; set; }
        public double FinalScore { get; set; }
    }
}
