using System;
using System.ComponentModel.DataAnnotations;

namespace ARCompletions.Data
{
    public class LineUser
    {
        [Key]
        public long Id { get; set; }
        public string LineUserId { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? PictureUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
        public bool IsFollowed { get; set; } = true;
    }
}
