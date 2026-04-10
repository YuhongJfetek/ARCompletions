using System;
using System.Threading;
using System.Threading.Tasks;

namespace ARCompletions.Services;

public interface IDistributedLock : IAsyncDisposable
{
    /// <summary>
    /// Try to acquire a lock for the given key. Returns true if acquired.
    /// </summary>
    Task<bool> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Release the currently held lock.
    /// </summary>
    Task ReleaseAsync();
}
