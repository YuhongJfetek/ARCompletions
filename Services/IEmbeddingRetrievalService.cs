using System;
using System.Threading.Tasks;

namespace ARCompletions.Services
{
    public interface IEmbeddingRetrievalService
    {
        // provider: e.g. "local_hash" or an external provider identifier
        Task<double[]?> GetOrCreateEmbeddingAsync(string normalizedText, string modelName, string provider = "local_hash", System.Threading.CancellationToken cancellationToken = default);
    }
}
