using System;
using System.Threading.Tasks;

namespace ARCompletions.Services
{
    public interface IEmbeddingRetrievalService
    {
        Task<double[]?> GetOrCreateEmbeddingAsync(string normalizedText, string modelName);
    }
}
