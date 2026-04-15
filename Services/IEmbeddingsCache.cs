using System.Collections.Generic;
using System.Threading.Tasks;
using ARCompletions.Domain;

namespace ARCompletions.Services
{
    public interface IEmbeddingsCache
    {
        Task<(List<BotFaqEmbedding> Embeddings, Dictionary<string, BotFaqItem> FaqMap)> GetOrLoadAsync(string provider);
        Task RefreshAsync(string provider);
    }
}
