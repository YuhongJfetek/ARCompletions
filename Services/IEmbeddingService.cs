using System.Threading.Tasks;

namespace ARCompletions.Services;

public interface IEmbeddingService
{
    /// <summary>
    /// 呼叫 OpenAI Embeddings API，回傳 raw JSON 字串（會包含向量資料）。
    /// </summary>
    Task<string?> GetEmbeddingJsonAsync(string input, string model);
}
