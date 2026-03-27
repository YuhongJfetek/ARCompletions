using System.Collections.Generic;
using ARCompletions.Domain;

namespace ARCompletions.Areas.Admin.Models
{
    public class BulkJobsIndexViewModel
    {
        public List<BulkJob> Items { get; set; } = new List<BulkJob>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long TotalCount { get; set; }
        public int TotalPages => PageSize <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
    }
}
