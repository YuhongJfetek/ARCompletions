using System;
using System.Threading;
using System.Threading.Tasks;
using ARCompletions.Domain;

namespace ARCompletions.Services;

public interface IEmbeddingRebuildService
{
    Task<BotEmbeddingJob> RebuildAsync(string provider, string? model, string scope, string? faqId, string triggeredBy, CancellationToken cancellationToken = default);

    Task<BotEmbeddingJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);
}
