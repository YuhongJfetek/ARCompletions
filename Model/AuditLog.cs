using System;
using System.ComponentModel.DataAnnotations;

namespace ARCompletions.Data
{
    public class AuditLog
    {
        [Key]
        public string Id { get; set; }

        public string Actor { get; set; }
        public string Action { get; set; }
        public string TargetId { get; set; }
        public string Payload { get; set; }
        public long Timestamp { get; set; }

        public AuditLog()
        {
            Id = Guid.NewGuid().ToString();
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
