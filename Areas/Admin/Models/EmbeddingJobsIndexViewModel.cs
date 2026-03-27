using System.Collections.Generic;
using ARCompletions.Domain;

namespace ARCompletions.Areas.Admin.Models
{
    public class EmbeddingJobsIndexViewModel
    {
        public List<EmbeddingJob> Items { get; set; } = new List<EmbeddingJob>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long TotalCount { get; set; }
        public int TotalPages => PageSize <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
        public string? VendorId { get; set; }
        public string? Status { get; set; }
        public string? VectorVersion { get; set; }
    }
}
