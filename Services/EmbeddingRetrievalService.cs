using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ARCompletions.Services
{
    public class EmbeddingRetrievalService : IEmbeddingRetrievalService
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<EmbeddingRetrievalService> _logger;

        public EmbeddingRetrievalService(IEmbeddingService embeddingService, IMemoryCache cache, ILogger<EmbeddingRetrievalService> logger)
        {
            _embeddingService = embeddingService;
            _cache = cache;
            _logger = logger;
        }

        public async Task<double[]?> GetOrCreateEmbeddingAsync(string normalizedText, string modelName)
        {
            if (normalizedText == null) return null;
            var cacheKeyVec = $"embedding_vec:{modelName}:{normalizedText}";
            var cacheTtlSeconds = int.TryParse(Environment.GetEnvironmentVariable("EMBEDDING_CACHE_TTL_SECONDS"), out var s) ? s : 300;
            if (_cache.TryGetValue<double[]>(cacheKeyVec, out var cachedVec))
            {
                _logger?.LogDebug("Embedding cache hit: key={Key} Len={Len}", cacheKeyVec, cachedVec?.Length ?? 0);
                if (cachedVec != null && cachedVec.Length > 0) return cachedVec;
                _logger?.LogDebug("Embedding cache contains empty marker for key={Key}", cacheKeyVec);
                return null;
            }

            string? embJson = null;
            try
            {
                embJson = await _embeddingService.GetEmbeddingJsonAsync(normalizedText, modelName);
                _logger?.LogDebug("Embedding service returned JSON length={Len} for model={Model}", embJson?.Length ?? 0, modelName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Embedding service call failed for model={Model}", modelName);
                embJson = null;
            }

            if (string.IsNullOrWhiteSpace(embJson))
            {
                var localPath = Environment.GetEnvironmentVariable("LOCAL_EMBEDDING_JSON");
                if (!string.IsNullOrWhiteSpace(localPath) && System.IO.File.Exists(localPath))
                {
                    try
                    {
                        using var fs = System.IO.File.OpenRead(localPath);
                        using var doc = JsonDocument.Parse(fs);
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var el in doc.RootElement.EnumerateArray())
                            {
                                if (el.TryGetProperty("text", out var t) && el.TryGetProperty("embedding", out var embEl))
                                {
                                    var txt = t.GetString() ?? string.Empty;
                                    if (string.Equals(txt.Trim().ToLowerInvariant(), normalizedText.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        var list = new List<double>();
                                        foreach (var v in embEl.EnumerateArray()) list.Add(v.GetDouble());
                                        var arr = list.ToArray();
                                        _cache.Set(cacheKeyVec, arr, TimeSpan.FromSeconds(cacheTtlSeconds));
                                        _logger?.LogInformation("Loaded embedding from local file for text. Model={Model} Len={Len}", modelName, arr.Length);
                                        return arr;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Failed to read local embedding JSON from {Path}", localPath);
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(embJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(embJson);
                    if (doc.RootElement.TryGetProperty("data", out var dataElem) && dataElem.GetArrayLength() > 0)
                    {
                        var embElem = dataElem[0].GetProperty("embedding");
                        var list = new List<double>();
                        foreach (var v in embElem.EnumerateArray()) list.Add(v.GetDouble());
                        if (list.Count > 0)
                        {
                            var arr = list.ToArray();
                            _cache.Set(cacheKeyVec, arr, TimeSpan.FromSeconds(cacheTtlSeconds));
                            _logger?.LogInformation("Parsed embedding JSON and cached: Model={Model} Len={Len}", modelName, arr.Length);
                            return arr;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to parse embedding JSON for model={Model}", modelName);
                }
            }

            _logger?.LogDebug("Setting empty embedding marker for key={Key}", cacheKeyVec);
            _cache.Set(cacheKeyVec, Array.Empty<double>(), TimeSpan.FromSeconds(Math.Min(30, cacheTtlSeconds)));
            return null;
        }
    }
}
