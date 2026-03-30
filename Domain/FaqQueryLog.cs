using System;

namespace ARCompletions.Domain
{
    public class FaqQueryLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string? MessageResultId { get; set; }
        public string? QueryText { get; set; }
        public string? MatchedFaqId { get; set; }
        public double? Confidence { get; set; }
        public string? DetailsJson { get; set; }
        public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
