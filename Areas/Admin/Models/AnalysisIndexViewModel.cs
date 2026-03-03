using System.Collections.Generic;

namespace ARCompletions.Areas.Admin.Models
{
    public class AnalysisIndexViewModel
    {
        public IEnumerable<Data.AnalysisJob> Jobs { get; set; } = new List<Data.AnalysisJob>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize > 0 ? (int)System.Math.Ceiling((double)TotalCount / PageSize) : 0;

        // Filtering / search
        public string? SearchQuery { get; set; }
        public string? FilterType { get; set; }
        public string? FilterStatus { get; set; }

        // Date range
        public System.DateTime? StartDate { get; set; }
        public System.DateTime? EndDate { get; set; }

        // JSON param inspection (naive substring matching)
        public string? JsonKey { get; set; }
        public string? JsonValue { get; set; }

        public IEnumerable<string> Types { get; set; } = new List<string>();
        public IEnumerable<string> Statuses { get; set; } = new List<string>();

        public int[] PageSizeOptions { get; set; } = new[] { 10, 25, 50, 100 };
    }
}
