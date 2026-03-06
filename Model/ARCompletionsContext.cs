using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.IO;
using System.Net;

namespace ARCompletions.Data
{
    public class ARCompletionsContext : DbContext, IDesignTimeDbContextFactory<ARCompletionsContext>
    {
        public ARCompletionsContext(DbContextOptions<ARCompletionsContext> options)
            : base(options)
        {
        }

        // Parameterless constructor to support design-time activation if needed
        public ARCompletionsContext()
            : base(BuildOptions())
        {
        }

        private static DbContextOptions<ARCompletionsContext> BuildOptions()
        {
            var builder = new DbContextOptionsBuilder<ARCompletionsContext>();

            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            var isPostgres = !string.IsNullOrWhiteSpace(databaseUrl);

            if (isPostgres)
            {
                var uri = new Uri(databaseUrl!);
                var userInfoParts = uri.UserInfo.Split(':', 2);
                var user = WebUtility.UrlDecode(userInfoParts[0]);
                var pass = userInfoParts.Length > 1 ? WebUtility.UrlDecode(userInfoParts[1]) : "";
                var host = uri.Host;
                var portNum = uri.Port == -1 ? 5432 : uri.Port;
                var dbName = uri.AbsolutePath.Trim('/');
                var pgConn =
                    $"Host={host};Port={portNum};Database={dbName};Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true";
                builder.UseNpgsql(pgConn);
            }
            else
            {
                string dbPath;
                var envDbPath = Environment.GetEnvironmentVariable("DB_PATH");
                if (!string.IsNullOrWhiteSpace(envDbPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(envDbPath)!);
                    dbPath = envDbPath!;
                }
                else
                {
                    var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
                    Directory.CreateDirectory(dataDir);
                    dbPath = Path.Combine(dataDir, "ARCompletions.db");
                }

                builder.UseSqlite($"Data Source={dbPath}");
            }

            return builder.Options;
        }

        // Design-time factory method (used by dotnet ef)
        public ARCompletionsContext CreateDbContext(string[] args)
        {
            return new ARCompletionsContext(BuildOptions());
        }

        public DbSet<Completion> Completions { get; set; }
        public DbSet<LineEvent> LineEvents { get; set; }
        public DbSet<LineUser> LineUsers { get; set; }
        public DbSet<LineEventLog> LineEventLogs { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<AnalysisJob> AnalysisJobs { get; set; }
        public DbSet<AnalysisDetail> AnalysisDetails { get; set; }
        public DbSet<ChatEmbedding> ChatEmbeddings { get; set; }
        public DbSet<UnmatchedQuery> UnmatchedQueries { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Indexes for query performance
            modelBuilder.Entity<ChatMessage>().HasIndex(c => c.UserId);
            modelBuilder.Entity<LineEvent>().HasIndex(e => e.EventId).IsUnique(false);
            modelBuilder.Entity<LineUser>().HasIndex(u => u.LineUserId).IsUnique();
            modelBuilder.Entity<LineEventLog>().HasIndex(l => l.LineUserId);
            modelBuilder.Entity<AnalysisJob>().HasIndex(j => j.Status);
            modelBuilder.Entity<UnmatchedQuery>().HasIndex(u => u.CreatedAt);
            modelBuilder.Entity<Feedback>().HasIndex(f => f.CreatedAt);
        }
    }
}
