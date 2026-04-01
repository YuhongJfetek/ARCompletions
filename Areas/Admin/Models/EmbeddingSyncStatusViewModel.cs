using System.Collections.Generic;

namespace ARCompletions.Areas.Admin.Models
{
    public class EmbeddingSyncStatusItem
    {
        public string FaqId { get; set; } = default!;
        public string VendorId { get; set; } = default!;
        public string Question { get; set; } = default!;
        public long FaqUpdatedAt { get; set; }
        public long? LastBuiltAt { get; set; }
    }

    public class EmbeddingSyncStatusViewModel
    {
        public List<EmbeddingSyncStatusItem> Items { get; set; } = new List<EmbeddingSyncStatusItem>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long TotalCount { get; set; }
        public int TotalPages => PageSize <= 0 ? 1 : (int)((TotalCount + PageSize - 1) / PageSize);
        public string? VendorId { get; set; }
    }
}
