using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using ARCompletions.Data;
using ARCompletions.Domain;

namespace ARCompletions.Services
{
    public class EmbeddingRetrievalService : IEmbeddingRetrievalService
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly IMemoryCache _cache;
        private readonly ARCompletionsContext _db;
        private readonly IDbLogger _dbLogger;
        private readonly ITextProcessingService _textProcessing;

        public EmbeddingRetrievalService(IEmbeddingService embeddingService, IMemoryCache cache, ARCompletionsContext db, IDbLogger dbLogger, ITextProcessingService textProcessing)
        {
            _embeddingService = embeddingService;
            _cache = cache;
            _db = db;
            _dbLogger = dbLogger;
            _textProcessing = textProcessing;
        }

        

        public async Task<double[]?> GetOrCreateEmbeddingAsync(string normalizedText, string modelName, string provider = "local_hash")
        {
            if (normalizedText == null) return null;
            var cacheKeyVec = $"embedding_vec:{provider}:{modelName}:{normalizedText}";
            var cacheTtlSeconds = int.TryParse(Environment.GetEnvironmentVariable("EMBEDDING_CACHE_TTL_SECONDS"), out var s) ? s : 300;
            if (_cache.TryGetValue<double[]>(cacheKeyVec, out var cachedVec))
            {
                await _dbLogger.LogAsync("Debug", "Embedding cache hit", new { Key = cacheKeyVec, Len = cachedVec?.Length ?? 0 });
                if (cachedVec != null && cachedVec.Length > 0) return cachedVec;
                await _dbLogger.LogAsync("Debug", "Embedding cache contains empty marker", new { Key = cacheKeyVec });
                return null;
            }

            // If provider is local_hash, compute embedding using project-local hash-based n-gram method.
            if (string.Equals(provider, "local_hash", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    // Build vector following project spec: char 1-3 grams + term tokens, dimension 64
                    const int VECTOR_DIMENSION = 64;
                    double[] vec = new double[VECTOR_DIMENSION];

                    string normalized = _textProcessing.Normalize(normalizedText) ?? string.Empty;

                    // tokenize n-grams (char 1..3)
                    var ngrams = new List<string>();
                    if (!string.IsNullOrEmpty(normalized))
                    {
                        var len = normalized.Length;
                        for (int n = 1; n <= 3; n++)
                        {
                            if (len < n) continue;
                            for (int i = 0; i <= len - n; i++)
                            {
                                ngrams.Add(normalized.Substring(i, n));
                            }
                        }
                    }

                    // helper FNV-like hash
                    static uint FnvlHash(string s)
                    {
                        uint hash = 2166136261u;
                        foreach (var ch in s)
                        {
                            hash ^= (uint)ch;
                            hash = unchecked(hash * 16777619u);
                        }
                        return hash;
                    }

                    void AddToken(double[] vector, string token, double weight)
                    {
                        var h = FnvlHash(token);
                        var idx = (int)(h % (uint)VECTOR_DIMENSION);
                        var sign = (h % 2u) == 0u ? 1.0 : -1.0;
                        vector[idx] += weight * sign;
                    }

                    // ngram tokens weight = 1
                    foreach (var t in ngrams) AddToken(vec, "qg:" + t, 1.0);

                    // term tokens: use textProcessing.Tokenize to get term-level tokens
                    var termTokens = _textProcessing.Tokenize(normalizedText) ?? Array.Empty<string>();
                    foreach (var term in termTokens)
                    {
                        AddToken(vec, "qt:" + term, 1.4);
                    }

                    // L2 normalize
                    double mag = 0;
                    for (int i = 0; i < vec.Length; i++) mag += vec[i] * vec[i];
                    mag = Math.Sqrt(mag);
                    if (mag > 0)
                    {
                        for (int i = 0; i < vec.Length; i++) vec[i] = vec[i] / mag;
                    }

                    _cache.Set(cacheKeyVec, vec, TimeSpan.FromSeconds(cacheTtlSeconds));
                    await _dbLogger.LogAsync("Information", "Computed local_hash embedding and cached", new { Provider = provider, Model = modelName, Len = vec.Length });
                    return vec;
                }
                catch (Exception ex)
                {
                    await _dbLogger.LogAsync("Warning", "Local hash embedding computation failed", new { Text = normalizedText }, ex);
                    _cache.Set(cacheKeyVec, Array.Empty<double>(), TimeSpan.FromSeconds(Math.Min(30, cacheTtlSeconds)));
                    return null;
                }
            }

            string? embJson = null;
            try
            {
                embJson = await _embeddingService.GetEmbeddingJsonAsync(normalizedText, modelName);
                await _dbLogger.LogAsync("Debug", "Embedding service returned JSON", new { Len = embJson?.Length ?? 0, Model = modelName });
            }
            catch (Exception ex)
            {
                await _dbLogger.LogAsync("Warning", "Embedding service call failed", new { Model = modelName }, ex);
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
                                        await _dbLogger.LogAsync("Information", "Loaded embedding from local file", new { Model = modelName, Len = arr.Length });
                                        return arr;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await _dbLogger.LogAsync("Warning", "Failed to read local embedding JSON", new { Path = localPath }, ex);
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
                            await _dbLogger.LogAsync("Information", "Parsed embedding JSON and cached", new { Model = modelName, Len = arr.Length });
                            return arr;
                        }
                    }
                }
                catch (Exception ex)
                {
                    await _dbLogger.LogAsync("Warning", "Failed to parse embedding JSON", new { Model = modelName }, ex);
                }
            }

            await _dbLogger.LogAsync("Debug", "Setting empty embedding marker", new { Key = cacheKeyVec });
            _cache.Set(cacheKeyVec, Array.Empty<double>(), TimeSpan.FromSeconds(Math.Min(30, cacheTtlSeconds)));
            return null;
        }
    }
}
