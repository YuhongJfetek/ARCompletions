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
    private readonly IDbLogger _dbLogger;
    private readonly IBufferedAppLogger? _bufferedLogger;

    public EmbeddingService(IHttpClientFactory httpFactory, IConfiguration config, IDbLogger dbLogger, IBufferedAppLogger? bufferedLogger = null)
    {
        _httpFactory = httpFactory;
        _config = config;
        _dbLogger = dbLogger;
        _bufferedLogger = bufferedLogger;
    }

        public async Task<string> GetEmbeddingJsonAsync(string input, string model, ARCompletionsContext? db = null)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var apiKey = _config["OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (db != null) await _dbLogger.LogAsync(db, "Error", "OpenAI API key not configured");
            else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Error", "OpenAI API key not configured");
            return null;
        }

        var client = _httpFactory.CreateClient("OpenAI");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new { model = model ?? "text-embedding-3-small", input };
        var json = JsonSerializer.Serialize(payload);

        // exponential backoff with jitter
        var maxAttempts = 6;
        var attempt = 0;
        var rng = new Random();
        while (attempt < maxAttempts)
        {
            attempt++;
            try
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
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
                    if (db != null) await _dbLogger.LogAsync(db, "Warning", "OpenAI embedding transient failure", new { Status = resp.StatusCode, Attempt = attempt, Max = maxAttempts, Delay = waitMs, Resp = respText }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Warning", "OpenAI embedding transient failure", new { Status = resp.StatusCode, Attempt = attempt, Max = maxAttempts, Delay = waitMs, Resp = respText });
                    await Task.Delay(waitMs);
                    continue;
                }

                if (db != null) await _dbLogger.LogAsync(db, "Error", "OpenAI embedding failed (non-transient)", new { Status = resp.StatusCode, Resp = respText }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Error", "OpenAI embedding failed (non-transient)", new { Status = resp.StatusCode, Resp = respText });
                return respText;
            }
            catch (Exception ex)
            {
                var waitMs = Math.Min(1000 * attempt * attempt, 30000);
                    if (db != null) await _dbLogger.LogAsync(db, "Warning", "Exception calling OpenAI embeddings", new { Attempt = attempt, Max = maxAttempts, Delay = waitMs }, ex); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Warning", "Exception calling OpenAI embeddings", new { Attempt = attempt, Max = maxAttempts, Delay = waitMs });
                await Task.Delay(waitMs);
            }
        }

        if (db != null) await _dbLogger.LogAsync(db, "Error", "OpenAI embedding request failed after attempts", new { Max = maxAttempts }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Error", "OpenAI embedding request failed after attempts", new { Max = maxAttempts });
        return null;
    }
}
