using System.Collections.Generic;

namespace ARCompletions.Areas.Admin.Models
{
    public class UnmatchedIndexViewModel
    {
        public IEnumerable<Data.UnmatchedQuery> Items { get; set; } = new List<Data.UnmatchedQuery>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => PageSize > 0 ? (int)System.Math.Ceiling((double)TotalCount / PageSize) : 0;
        public string? Query { get; set; }
        public int[] PageSizeOptions { get; set; } = new[] { 10, 25, 50, 100 };
    }
}
