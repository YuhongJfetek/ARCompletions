using System.Collections.Generic;
using ARCompletions.Domain;

namespace ARCompletions.Areas.Vendor.Models
{
    public class VendorFaqCandidatesIndexViewModel
    {
        public List<FaqCandidate> Items { get; set; } = new List<FaqCandidate>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long TotalCount { get; set; }
        public int TotalPages => PageSize <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? CategoryId { get; set; }
    }
}
