using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ARCompletions.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<EmbeddingService> logger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _logger = logger;
    }

    public async Task<string?> GetEmbeddingJsonAsync(string input, string model)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var apiKey = _config["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("OpenAI API key not configured (OpenAI:ApiKey or OPENAI_API_KEY)");
            return null;
        }

        var client = _httpFactory.CreateClient("OpenAI");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new { model = model ?? "text-embedding-3-small", input };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // exponential backoff with jitter
        var maxAttempts = 6;
        var attempt = 0;
        var rng = new Random();
        while (attempt < maxAttempts)
        {
            attempt++;
            try
            {
                var resp = await client.PostAsync("v1/embeddings", content);
                var respText = await resp.Content.ReadAsStringAsync();
                if (resp.IsSuccessStatusCode)
                {
                    return respText;
                }

                if ((int)resp.StatusCode == 429 || ((int)resp.StatusCode >= 500 && (int)resp.StatusCode < 600))
                {
                    var baseDelay = Math.Min(2000 * attempt, 30000);
                    var jitter = rng.Next(0, 500);
                    var waitMs = baseDelay + jitter;
                    _logger.LogWarning("OpenAI embedding request transient failure {Status}. Retry {Attempt}/{Max} after {Delay}ms. Response: {Resp}", resp.StatusCode, attempt, maxAttempts, waitMs, respText);
                    await Task.Delay(waitMs);
                    continue;
                }

                _logger.LogError("OpenAI embedding request failed (non-transient): {Status} {Resp}", resp.StatusCode, respText);
                return respText;
            }
            catch (Exception ex)
            {
                var waitMs = Math.Min(1000 * attempt * attempt, 30000);
                _logger.LogWarning(ex, "Exception calling OpenAI embeddings (attempt {Attempt}/{Max}). Retrying after {Delay}ms", attempt, maxAttempts, waitMs);
                await Task.Delay(waitMs);
            }
        }

        _logger.LogError("OpenAI embedding request failed after {Max} attempts", maxAttempts);
        return null;
    }
}
