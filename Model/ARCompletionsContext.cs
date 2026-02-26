using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Data
{
    public class ARCompletionsContext : DbContext
    {
        public ARCompletionsContext(DbContextOptions<ARCompletionsContext> options)
            : base(options)
        {
        }

        public DbSet<Completion> Completions { get; set; }
        public DbSet<LineEvent> LineEvents { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<AnalysisJob> AnalysisJobs { get; set; }
        public DbSet<AnalysisDetail> AnalysisDetails { get; set; }
        public DbSet<ChatEmbedding> ChatEmbeddings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Indexes for query performance
            modelBuilder.Entity<ChatMessage>().HasIndex(c => c.UserId);
            modelBuilder.Entity<LineEvent>().HasIndex(e => e.EventId).IsUnique(false);
            modelBuilder.Entity<AnalysisJob>().HasIndex(j => j.Status);
        }
    }
}
