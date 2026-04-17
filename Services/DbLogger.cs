using System;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;

namespace ARCompletions.Services
{
    public class DbLogger : IDbLogger
    {
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> _dbFactory;

        public DbLogger(Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task LogAsync(string level, string message, object? properties = null, Exception? ex = null, bool deferSave = false)
        {
            try
            {
                var log = new AppLog
                {
                    Id = Guid.NewGuid().ToString(),
                    TimeStamp = DateTime.UtcNow,
                    Level = level,
                    Message = message,
                    MessageTemplate = message,
                    Exception = ex?.ToString(),
                    Properties = properties == null ? null : System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(properties))
                };
                using var db = _dbFactory.CreateDbContext();
                db.AppLogs.Add(log);
                await db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                try
                {
                    Console.Error.WriteLine("DbLogger.LogAsync: failed to write AppLog - " + e.ToString());
                }
                catch
                {
                }
            }
        }

        public void LogSync(string level, string message, object? properties = null, Exception? ex = null, bool deferSave = false)
        {
            try
            {
                var log = new AppLog
                {
                    Id = Guid.NewGuid().ToString(),
                    TimeStamp = DateTime.UtcNow,
                    Level = level,
                    Message = message,
                    MessageTemplate = message,
                    Exception = ex?.ToString(),
                    Properties = properties == null ? null : System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(properties))
                };
                using var db = _dbFactory.CreateDbContext();
                db.AppLogs.Add(log);
                db.SaveChanges();
            }
            catch (Exception e)
            {
                try
                {
                    Console.Error.WriteLine("DbLogger.LogSync: failed to write AppLog - " + e.ToString());
                }
                catch
                {
                }
            }
        }
    }
}
