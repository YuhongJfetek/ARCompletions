using System.Threading.Tasks;

namespace ARCompletions.Services
{
    public class NoopBufferedAppLogger : IBufferedAppLogger
    {
        public ValueTask EnqueueLogAsync(string level, string message, object? props = null)
        {
            return ValueTask.CompletedTask;
        }
    }
}
