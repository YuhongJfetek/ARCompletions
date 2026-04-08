using System;
using System.Threading.Tasks;

namespace ARCompletions.Services
{
    public interface IDbLogger
    {
        Task LogAsync(string level, string message, object? properties = null, Exception? ex = null);
        void LogSync(string level, string message, object? properties = null, Exception? ex = null);
    }
}
