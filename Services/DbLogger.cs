using System;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;

namespace ARCompletions.Services
{
    public class DbLogger : IDbLogger
    {
        private readonly ARCompletionsContext _db;

        public DbLogger(ARCompletionsContext db)
        {
            _db = db;
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
                    Properties = properties == null ? null : System.Text.Json.JsonSerializer.Serialize(properties)
                };
                _db.AppLogs.Add(log);
                await _db.SaveChangesAsync();
            }
            catch (Exception e)
            {
                try
                {
                    Console.Error.WriteLine("DbLogger.LogAsync: failed to write AppLog - " + e.ToString());
                }
                catch
                {
                    // best-effort: don't throw from logger
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
                    Properties = properties == null ? null : System.Text.Json.JsonSerializer.Serialize(properties)
                };
                _db.AppLogs.Add(log);
                _db.SaveChanges();
            }
            catch (Exception e)
            {
                try
                {
                    Console.Error.WriteLine("DbLogger.LogSync: failed to write AppLog - " + e.ToString());
                }
                catch
                {
                    // best-effort: don't throw from logger
                }
            }
        }
    }
}
