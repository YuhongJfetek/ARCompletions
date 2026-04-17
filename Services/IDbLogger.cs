using System;
using System.Threading.Tasks;
using ARCompletions.Data;

namespace ARCompletions.Services
{
    public interface IDbLogger
    {
        Task LogAsync(string level, string message, object? properties = null, Exception? ex = null, bool deferSave = false);
        Task LogAsync(ARCompletionsContext db, string level, string message, object? properties = null, Exception? ex = null, bool saveNow = false);
        void LogSync(string level, string message, object? properties = null, Exception? ex = null, bool deferSave = false);
    }
}
