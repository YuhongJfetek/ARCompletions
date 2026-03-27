using System.Collections.Generic;
using ARCompletions.Domain;

namespace ARCompletions.Areas.Vendor.Models
{
    public class VendorFaqsIndexViewModel
    {
        public List<Faq> Items { get; set; } = new List<Faq>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long TotalCount { get; set; }
        public int TotalPages => PageSize <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
        public string? Search { get; set; }
        public string? CategoryId { get; set; }
        public string? Status { get; set; }
        public IEnumerable<ARCompletions.Domain.FaqCategory>? Categories { get; set; }
    }
}
