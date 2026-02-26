using System.Threading.Tasks;

namespace ARCompletions.Services
{
    public interface IEmbeddingService
    {
        Task<double[]> ComputeEmbeddingAsync(string text, int dim, string? apiKey);
    }
}
