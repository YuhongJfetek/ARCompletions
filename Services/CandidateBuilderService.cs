using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ARCompletions.Services
{
    public class CandidateBuilderService : ICandidateBuilderService
    {
        private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> _dbFactory;
        private readonly IDbLogger _dbLogger;
        private readonly IBufferedAppLogger? _bufferedLogger;
        private readonly IScoringService _scoring;
        private readonly IEmbeddingUpdateQueue _updateQueue;
        private readonly IEmbeddingsCache _embeddingsCache;
        private readonly ITextProcessingService _textProcessing;

        public CandidateBuilderService(Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> dbFactory, IScoringService scoring, IDbLogger dbLogger, IEmbeddingUpdateQueue updateQueue, IEmbeddingsCache embeddingsCache, ITextProcessingService textProcessing, IBufferedAppLogger? bufferedLogger = null)
        {
            _dbFactory = dbFactory;
            _scoring = scoring;
            _dbLogger = dbLogger;
            _bufferedLogger = bufferedLogger;
            _updateQueue = updateQueue;
            _embeddingsCache = embeddingsCache;
            _textProcessing = textProcessing;
        }

        public async Task<List<string>> BuildCandidatesAsync(string normalizedText, double[]? queryVec, string embeddingProvider, int topN = 5, ARCompletionsContext? db = null)
        {
            var sw = Stopwatch.StartNew();
            if (db != null) await _dbLogger.LogAsync(db, "Debug", "BuildCandidatesAsync start", new { Len = (normalizedText ?? string.Empty).Length, HasVec = queryVec != null, Provider = embeddingProvider, TopN = topN }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "BuildCandidatesAsync start", new { Len = (normalizedText ?? string.Empty).Length, HasVec = queryVec != null, Provider = embeddingProvider, TopN = topN });

            try
            {
                // load embedding items and faq map (use process-level cache)
                var t0 = sw.ElapsedMilliseconds;
                var (embItems, faqMap) = await _embeddingsCache.GetOrLoadAsync(embeddingProvider);
                var t1 = sw.ElapsedMilliseconds;
                if (db != null) await _dbLogger.LogAsync(db, "Debug", "Embeddings loaded (cache)", new { Count = embItems.Count, ElapsedMs = t1 - t0 }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "Embeddings loaded (cache)", new { Count = embItems.Count, ElapsedMs = t1 - t0 });
                if (embItems.Count == 0)
                {
                    if (db != null) await _dbLogger.LogAsync(db, "Information", "No embedding items for provider (cache)", new { Provider = embeddingProvider }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Information", "No embedding items for provider (cache)", new { Provider = embeddingProvider });
                    return new List<string>();
                }

                var t3Start = sw.ElapsedMilliseconds;
                // metadata prefilter: try to reduce candidate set by category/subcategory/keywords matches
                var maxCandidates = int.TryParse(Environment.GetEnvironmentVariable("EMBEDDING_MAX_CANDIDATES"), out var mc) ? mc : 150;
                var normalizedLower = (normalizedText ?? string.Empty).ToLowerInvariant();
                var rawTokens = _textProcessing.Tokenize(normalizedText ?? string.Empty) ?? Array.Empty<string>();
                string[] tokens = rawTokens.Select(t => (t ?? string.Empty).ToLowerInvariant()).ToArray();

                var highPriority = new List<BotFaqItem>();
                var others = new List<BotFaqItem>();
                foreach (var f in faqMap.Values)
                {
                    double[]? v = null;
                    var emb = embItems.FirstOrDefault(e => e.FaqId == f.FaqId && (e.Embedding?.Length ?? 0) > 0);
                    if (emb != null) v = emb.Embedding;
                    else
                    {
                        // If provider embedding missing, enqueue a background update (non-blocking)
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(embeddingProvider) && !string.Equals(embeddingProvider, "local_hash", StringComparison.OrdinalIgnoreCase))
                            {
                                var textToEmbed = f.Question ?? f.SearchTextCache ?? string.Empty;
                                _ = _updateQueue.EnqueueAsync(new EmbeddingUpdateRequest(f.FaqId, textToEmbed, embeddingProvider, null));
                            }
                        }
                        catch { }
                    }
                    // decide priority based on metadata
                    var metaMatched = false;
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(f.CategoryKey) && normalizedLower.Contains(f.CategoryKey!.ToLowerInvariant())) metaMatched = true;
                        if (!metaMatched && !string.IsNullOrWhiteSpace(f.Category) && normalizedLower.Contains(f.Category!.ToLowerInvariant())) metaMatched = true;
                        if (!metaMatched && !string.IsNullOrWhiteSpace(f.Subcategory) && normalizedLower.Contains(f.Subcategory!.ToLowerInvariant())) metaMatched = true;
                        if (!metaMatched && !string.IsNullOrWhiteSpace(f.Keywords))
                        {
                            var kw = f.Keywords!.ToLowerInvariant();
                            foreach (var t in tokens)
                            {
                                if (string.IsNullOrWhiteSpace(t)) continue;
                                if (kw.Contains(t)) { metaMatched = true; break; }
                            }
                        }
                    }
                    catch { metaMatched = false; }

                    if (metaMatched) highPriority.Add(f);
                    else others.Add(f);
                }
                // build final candidate list: prefer highPriority, then fill from others by UpdatedAt desc until maxCandidates
                var selectedFaqs = new List<BotFaqItem>();
                if (highPriority.Count > 0)
                {
                    selectedFaqs.AddRange(highPriority);
                }

                if (selectedFaqs.Count < maxCandidates)
                {
                    var needed = maxCandidates - selectedFaqs.Count;
                    var fill = others.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt).Take(needed);
                    selectedFaqs.AddRange(fill);
                }

                var candidateTuples = new List<(string FaqId, double[]? Vec, string Question, string SearchTextCache)>();
                foreach (var f in selectedFaqs)
                {
                    var emb = embItems.FirstOrDefault(e => e.FaqId == f.FaqId && (e.Embedding?.Length ?? 0) > 0);
                    double[]? v = emb != null ? emb.Embedding : null;
                    candidateTuples.Add((f.FaqId, v, f.Question ?? string.Empty, f.SearchTextCache ?? string.Empty));
                }
                var t3 = sw.ElapsedMilliseconds;
                if (db != null) await _dbLogger.LogAsync(db, "Debug", "Candidate tuples prepared (prefilter)", new { TotalFaqs = faqMap.Count, Selected = candidateTuples.Count, HighPriority = highPriority.Count, ElapsedMs = t3 - t3Start }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "Candidate tuples prepared (prefilter)", new { TotalFaqs = faqMap.Count, Selected = candidateTuples.Count, HighPriority = highPriority.Count, ElapsedMs = t3 - t3Start });

                var t4Start = sw.ElapsedMilliseconds;
                var scores = _scoring.ScoreCandidates(queryVec ?? Array.Empty<double>(), candidateTuples, normalizedText ?? string.Empty, faqMap);
                var t4 = sw.ElapsedMilliseconds;
                if (db != null) await _dbLogger.LogAsync(db, "Debug", "Scoring computed", new { CandidateCount = candidateTuples.Count, ElapsedMs = t4 - t4Start }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Debug", "Scoring computed", new { CandidateCount = candidateTuples.Count, ElapsedMs = t4 - t4Start });

                var t5Start = sw.ElapsedMilliseconds;
                var ranked = scores.OrderByDescending(kv => kv.Value).Select(kv => kv.Key).Take(topN).ToList();
                var t5 = sw.ElapsedMilliseconds;
                if (db != null) await _dbLogger.LogAsync(db, "Information", "Candidates built", new { ConversationTextLen = (normalizedText ?? string.Empty).Length, TopIds = ranked, TotalElapsedMs = sw.ElapsedMilliseconds, StepLoadEmbMs = t1 - t0, StepLoadFaqMs = 0, StepPrepareTuplesMs = t3 - t3Start, StepScoringMs = t4 - t4Start, StepRankMs = t5 - t5Start }); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Information", "Candidates built", new { ConversationTextLen = (normalizedText ?? string.Empty).Length, TopIds = ranked, TotalElapsedMs = sw.ElapsedMilliseconds, StepLoadEmbMs = t1 - t0, StepLoadFaqMs = 0, StepPrepareTuplesMs = t3 - t3Start, StepScoringMs = t4 - t4Start, StepRankMs = t5 - t5Start });

                return ranked;
            }
            catch (Exception ex)
            {
                if (db != null) await _dbLogger.LogAsync(db, "Warning", "CandidateBuilder.BuildCandidatesAsync failed", new { Text = normalizedText }, ex); else if (_bufferedLogger != null) await _bufferedLogger.EnqueueLogAsync("Warning", "CandidateBuilder.BuildCandidatesAsync failed", new { Text = normalizedText });
                throw;
            }
        }
    }
}
