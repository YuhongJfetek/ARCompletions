using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ARCompletions.Data;
using ARCompletions.Domain;

namespace ARCompletions.Services
{
    public class EmbeddingRetrievalService : IEmbeddingRetrievalService
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly IMemoryCache _cache;
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> _dbFactory;
        private readonly IDbLogger _dbLogger;
        private readonly IBufferedAppLogger? _bufferedLogger;
        private readonly ITextProcessingService _textProcessing;
        private readonly Func<IDistributedLock> _distributedLockFactory;

        public EmbeddingRetrievalService(IEmbeddingService embeddingService, IMemoryCache cache, Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> dbFactory, IDbLogger dbLogger, ITextProcessingService textProcessing, Func<IDistributedLock> distributedLockFactory, IBufferedAppLogger? bufferedLogger = null)
        {
            _embeddingService = embeddingService;
            _cache = cache;
            _dbFactory = dbFactory;
            _dbLogger = dbLogger;
            _bufferedLogger = bufferedLogger;
            _textProcessing = textProcessing;
            _distributedLockFactory = distributedLockFactory;
        }

        

        public async Task<double[]?> GetOrCreateEmbeddingAsync(string normalizedText, string modelName, string provider = "local_hash", System.Threading.CancellationToken cancellationToken = default, ARCompletionsContext? db = null)
        {
            if (normalizedText == null) return null;
            var cacheKeyVec = $"embedding_vec:{provider}:{modelName}:{normalizedText}";
            var overallSw = Stopwatch.StartNew();
            if (db != null) await _dbLogger.LogAsync(db, "Debug", "GetOrCreateEmbeddingAsync START", new { Provider = provider, Model = modelName, Key = cacheKeyVec, TextLen = normalizedText.Length }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "GetOrCreateEmbeddingAsync START", new { Provider = provider, Model = modelName, Key = cacheKeyVec, TextLen = normalizedText.Length });
            var cacheTtlSeconds = int.TryParse(Environment.GetEnvironmentVariable("EMBEDDING_CACHE_TTL_SECONDS"), out var s) ? s : 300;
                if (_cache.TryGetValue<double[]>(cacheKeyVec, out var cachedVec))
            {
                if (db != null) await _dbLogger.LogAsync(db, "Debug", "Embedding cache hit", new { Key = cacheKeyVec, Len = cachedVec?.Length ?? 0 }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "Embedding cache hit", new { Key = cacheKeyVec, Len = cachedVec?.Length ?? 0 });
                if (cachedVec != null && cachedVec.Length > 0)
                {
                    if (db != null) await _dbLogger.LogAsync(db, "Debug", "GetOrCreateEmbeddingAsync END", new { Provider = provider, Model = modelName, Len = cachedVec.Length, ElapsedMs = overallSw.ElapsedMilliseconds }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "GetOrCreateEmbeddingAsync END", new { Provider = provider, Model = modelName, Len = cachedVec.Length, ElapsedMs = overallSw.ElapsedMilliseconds });
                    return cachedVec;
                }
                if (db != null) await _dbLogger.LogAsync(db, "Debug", "Embedding cache contains empty marker", new { Key = cacheKeyVec }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "Embedding cache contains empty marker", new { Key = cacheKeyVec }); else await _dbLogger.LogAsync("Debug", "Embedding cache contains empty marker", new { Key = cacheKeyVec });
                if (db != null) await _dbLogger.LogAsync(db, "Debug", "GetOrCreateEmbeddingAsync END", new { Provider = provider, Model = modelName, Len = 0, ElapsedMs = overallSw.ElapsedMilliseconds }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "GetOrCreateEmbeddingAsync END", new { Provider = provider, Model = modelName, Len = 0, ElapsedMs = overallSw.ElapsedMilliseconds });
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
                    if (db != null) await _dbLogger.LogAsync(db, "Information", "Computed local_hash embedding and cached", new { Provider = provider, Model = modelName, Len = vec.Length }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Information", "Computed local_hash embedding and cached", new { Provider = provider, Model = modelName, Len = vec.Length }); else await _dbLogger.LogAsync("Information", "Computed local_hash embedding and cached", new { Provider = provider, Model = modelName, Len = vec.Length });
                    if (db != null) await _dbLogger.LogAsync(db, "Debug", "GetOrCreateEmbeddingAsync END", new { Provider = provider, Model = modelName, Len = vec.Length, ElapsedMs = overallSw.ElapsedMilliseconds }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "GetOrCreateEmbeddingAsync END", new { Provider = provider, Model = modelName, Len = vec.Length, ElapsedMs = overallSw.ElapsedMilliseconds }); else await _dbLogger.LogAsync("Debug", "GetOrCreateEmbeddingAsync END", new { Provider = provider, Model = modelName, Len = vec.Length, ElapsedMs = overallSw.ElapsedMilliseconds });
                    return vec;
                }
                catch (Exception ex)
                {
                    if (db != null) await _dbLogger.LogAsync(db, "Warning", "Local hash embedding computation failed", new { Text = normalizedText }, ex); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Warning", "Local hash embedding computation failed", new { Text = normalizedText }); else await _dbLogger.LogAsync("Warning", "Local hash embedding computation failed", new { Text = normalizedText }, ex);
                    _cache.Set(cacheKeyVec, Array.Empty<double>(), TimeSpan.FromSeconds(Math.Min(30, cacheTtlSeconds)));
                    if (db != null) await _dbLogger.LogAsync(db, "Debug", "GetOrCreateEmbeddingAsync END", new { Provider = provider, Model = modelName, Len = 0, ElapsedMs = overallSw.ElapsedMilliseconds }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "GetOrCreateEmbeddingAsync END", new { Provider = provider, Model = modelName, Len = 0, ElapsedMs = overallSw.ElapsedMilliseconds }); else await _dbLogger.LogAsync("Debug", "GetOrCreateEmbeddingAsync END", new { Provider = provider, Model = modelName, Len = 0, ElapsedMs = overallSw.ElapsedMilliseconds });
                    return null;
                }
            }

            string? embJson = null;
                var checksumKey = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalizedText))).Substring(0,16);
                var lockKey = $"emb_lock:{provider}:{modelName}:{checksumKey}";
            IDistributedLock? dl = null;
            try
            {
                // Attempt to avoid duplicate provider calls by acquiring a distributed lock.
                dl = _distributedLockFactory();
                var acquired = await dl.TryAcquireAsync(lockKey, TimeSpan.FromSeconds(30), cancellationToken);
                    if (!acquired)
                    {
                        // someone else is generating; poll cache/DB until available or cancelled
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        while (!cancellationToken.IsCancellationRequested && sw.ElapsedMilliseconds < 5000)
                        {
                            if (_cache.TryGetValue<double[]>(cacheKeyVec, out var cv) && cv != null && cv.Length > 0)
                            {
                                if (db != null) await _dbLogger.LogAsync(db, "Information", "Embedding became available from other worker", new { Key = cacheKeyVec }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Information", "Embedding became available from other worker", new { Key = cacheKeyVec }); else await _dbLogger.LogAsync("Information", "Embedding became available from other worker", new { Key = cacheKeyVec });
                                return cv;
                            }
                            await Task.Delay(150, cancellationToken);
                        }
                        // fallback: attempt to call provider ourselves if still no value and not cancelled
                        if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
                        // try to acquire again (non-blocking)
                        acquired = await dl.TryAcquireAsync(lockKey, TimeSpan.Zero, cancellationToken);
                    }

                    if (acquired)
                    {
                        // we hold the lock and should call provider
                        embJson = await _embeddingService.GetEmbeddingJsonAsync(normalizedText, modelName, db);
                        if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "Embedding service returned JSON", new { Len = embJson?.Length ?? 0, Model = modelName }); else await _dbLogger.LogAsync("Debug", "Embedding service returned JSON", new { Len = embJson?.Length ?? 0, Model = modelName });
                    }
                    else
                    {
                        // last-resort: try to read from DB before giving up
                        if (db != null)
                        {
                            var fromDb = await db.BotFaqEmbeddings.AsNoTracking()
                                .FirstOrDefaultAsync(e => e.EmbeddingProvider == provider && e.EmbeddingModel == modelName && e.Embedding != null && e.Embedding.Length > 0, cancellationToken: cancellationToken);
                            if (fromDb != null)
                            {
                                var arr = fromDb.Embedding;
                                _cache.Set(cacheKeyVec, arr, TimeSpan.FromSeconds(cacheTtlSeconds));
                                await _dbLogger.LogAsync(db, "Debug", "GetOrCreateEmbeddingAsync END", new { Provider = provider, Model = modelName, Len = arr?.Length ?? 0, ElapsedMs = overallSw.ElapsedMilliseconds });
                                return arr;
                            }
                        }
                        else
                        {
                            using (var _db = _dbFactory.CreateDbContext())
                            {
                                var fromDb = await _db.BotFaqEmbeddings.AsNoTracking()
                                    .FirstOrDefaultAsync(e => e.EmbeddingProvider == provider && e.EmbeddingModel == modelName && e.Embedding != null && e.Embedding.Length > 0, cancellationToken: cancellationToken);
                                if (fromDb != null)
                                {
                                    var arr = fromDb.Embedding;
                                    _cache.Set(cacheKeyVec, arr, TimeSpan.FromSeconds(cacheTtlSeconds));
                                    if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "GetOrCreateEmbeddingAsync END", new { Provider = provider, Model = modelName, Len = arr?.Length ?? 0, ElapsedMs = overallSw.ElapsedMilliseconds });
                                    return arr;
                                }
                            }
                        }
                    }
            }
            catch (Exception ex)
            {
                    if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Warning", "Embedding service call failed or waiting cancelled", new { Model = modelName });
                    embJson = null;
            }
                finally
                {
                    if (dl != null)
                    {
                        try { await dl.ReleaseAsync(); } catch { }
                        try { await dl.DisposeAsync(); } catch { }
                    }
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
                                        if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Information", "Loaded embedding from local file", new { Model = modelName, Len = arr.Length });
                                        if (db != null) await _dbLogger.LogAsync(db, "Debug", "GetOrCreateEmbeddingAsync END", new { Provider = provider, Model = modelName, Len = arr.Length, ElapsedMs = overallSw.ElapsedMilliseconds }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "GetOrCreateEmbeddingAsync END", new { Provider = provider, Model = modelName, Len = arr.Length, ElapsedMs = overallSw.ElapsedMilliseconds });
                                        return arr;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Warning", "Failed to read local embedding JSON", new { Path = localPath });
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
                            if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Information", "Parsed embedding JSON and cached", new { Model = modelName, Len = arr.Length });
                            if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "GetOrCreateEmbeddingAsync END", new { Provider = provider, Model = modelName, Len = arr.Length, ElapsedMs = overallSw.ElapsedMilliseconds });
                            return arr;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Warning", "Failed to parse embedding JSON", new { Model = modelName });
                }
            }

            if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "Setting empty embedding marker", new { Key = cacheKeyVec });
            _cache.Set(cacheKeyVec, Array.Empty<double>(), TimeSpan.FromSeconds(Math.Min(30, cacheTtlSeconds)));
            if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "GetOrCreateEmbeddingAsync END", new { Provider = provider, Model = modelName, Len = 0, ElapsedMs = overallSw.ElapsedMilliseconds });
            return null;
        }
    }
}
