using System.Collections.Generic;
using ARCompletions.Domain;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ARCompletions.Areas.Vendor.Models
{
    public class VendorFaqCandidatesBatchViewModel
    {
        public List<FaqCandidate> Items { get; set; } = new List<FaqCandidate>();
        public List<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
        public string? SelectedCategoryId { get; set; }
        public string[]? SelectedIds { get; set; }
    }
}
