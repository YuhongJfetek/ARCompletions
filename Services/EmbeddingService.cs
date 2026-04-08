using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ARCompletions.Data;
using ARCompletions.Domain;

namespace ARCompletions.Services;

public class EmbeddingService : IEmbeddingService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;
    private readonly ARCompletionsContext _db;
    private readonly IDbLogger _dbLogger;

    public EmbeddingService(IHttpClientFactory httpFactory, IConfiguration config, ARCompletionsContext db, IDbLogger dbLogger)
    {
        _httpFactory = httpFactory;
        _config = config;
        _db = db;
        _dbLogger = dbLogger;
    }

    public async Task<string?> GetEmbeddingJsonAsync(string input, string model)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var apiKey = _config["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await _dbLogger.LogAsync("Error", "OpenAI API key not configured");
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
                    await _dbLogger.LogAsync("Warning", "OpenAI embedding transient failure", new { Status = resp.StatusCode, Attempt = attempt, Max = maxAttempts, Delay = waitMs, Resp = respText });
                    await Task.Delay(waitMs);
                    continue;
                }

                await _dbLogger.LogAsync("Error", "OpenAI embedding failed (non-transient)", new { Status = resp.StatusCode, Resp = respText });
                return respText;
            }
            catch (Exception ex)
            {
                var waitMs = Math.Min(1000 * attempt * attempt, 30000);
                    await _dbLogger.LogAsync("Warning", "Exception calling OpenAI embeddings", new { Attempt = attempt, Max = maxAttempts, Delay = waitMs }, ex);
                await Task.Delay(waitMs);
            }
        }

        await _dbLogger.LogAsync("Error", "OpenAI embedding request failed after attempts", new { Max = maxAttempts });
        return null;
    }
}
