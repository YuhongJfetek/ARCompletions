using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using ARCompletions.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Controllers.Api;

[ApiController]
[Route("internal/v1")] // 由 Program.cs 的 middleware 保護 X-Internal-API-Key
public class BotController : ControllerBase
{
    private readonly ARCompletionsContext _db;
    private readonly IMemoryCache _cache;
    private readonly ARCompletions.Services.IDbLogger _dbLogger;
    private readonly IDisambiguationService _disambiguationService;
    private readonly ITextProcessingService _textProcessing;
    private readonly IPrefilterService _prefilter;
    private readonly IStateService _stateService;
    private readonly IAliasService _aliasService;
    private readonly IFaqService _faqService;
    private readonly IEmbeddingRetrievalService _embeddingRetrieval;
    private readonly IScoringService _scoring;
    private readonly ICandidateBuilderService _candidateBuilder;
    private readonly IRouteLoggingService _routeLogger;
    private readonly IResponseBuilder _responseBuilder;

    public BotController(
        ARCompletionsContext db,
        IMemoryCache cache,
        IDisambiguationService disambiguationService,
        ARCompletions.Services.IDbLogger dbLogger,
        ITextProcessingService textProcessing,
        IPrefilterService prefilter,
        IStateService stateService,
        IAliasService aliasService,
        IFaqService faqService,
        IEmbeddingRetrievalService embeddingRetrieval,
        IScoringService scoring,
        ICandidateBuilderService candidateBuilder,
        IRouteLoggingService routeLogger,
        IResponseBuilder responseBuilder)
    {
        _db = db;
        _cache = cache;
        _dbLogger = dbLogger;
        _disambiguationService = disambiguationService;
        _textProcessing = textProcessing;
        _prefilter = prefilter;
        _stateService = stateService;
        _aliasService = aliasService;
        _faqService = faqService;
        _embeddingRetrieval = embeddingRetrieval;
        _scoring = scoring;
        _candidateBuilder = candidateBuilder;
        _routeLogger = routeLogger;
        _responseBuilder = responseBuilder;
    }

    // BotController now uses injected IDbLogger (`_dbLogger`) for DB-backed logging.

    private async Task WriteAppLogAsync(string level, string message, object? props = null, Exception? ex = null)
    {
        try
        {
            if (_dbLogger != null)
            {
                await _dbLogger.LogAsync(level, message, props, ex, deferSave: true);
            }
        }
        catch
        {
            // swallow to avoid interfering with request flow
        }
    }


    // A1 查詢決策 API（簡化版：目前僅回傳 shouldReply=false 骨架，後續可接上實際判斷流程）
    [HttpPost("bot/query")]
    public async Task<IActionResult> Query([FromBody] BotQueryRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.ConversationId))
        {
            return BadRequest(new { error = "invalid request" });
        }

        var sw = Stopwatch.StartNew();
        await _dbLogger.LogAsync("Information", "Query received: ConversationId={ConversationId} UserId={UserId} SourceType={SourceType} TextLen={TextLen}", new { ConversationId = req.ConversationId, UserId = req.UserId, SourceType = req.SourceType, TextLen = (req.Text ?? string.Empty).Length });

        var now = DateTimeOffset.UtcNow;
        var sourceType = string.IsNullOrWhiteSpace(req.SourceType) ? "group" : req.SourceType;

        // 可配置是否強制轉成 UTC（預設 true）
        var forceUtc = (Environment.GetEnvironmentVariable("FORCE_UTC") ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);
        var receivedAt = req.ReceivedAt == default
            ? now
            : (forceUtc ? req.ReceivedAt.ToUniversalTime() : req.ReceivedAt);

        // 1) 寫入 incoming event（可選擇 persist）
        var ev = new BotIncomingEvent
        {
            RawEventJson = req.RawEvent ?? "{}",
            EventType = "message",
            MessageType = "text",
            SourceType = sourceType,
            LineUserId = req.UserId,
            LineGroupId = req.GroupId,
            LineRoomId = req.RoomId,
            ConversationId = req.ConversationId,
            ReplyToken = req.ReplyToken,
            ReceivedAt = receivedAt
        };
        var persistIncoming = (Environment.GetEnvironmentVariable("PERSIST_INCOMING_EVENTS") ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);
        // defer SaveChanges where possible to reduce DB roundtrips
        var deferredSaveNeeded = false;
        if (persistIncoming)
        {
            _db.BotIncomingEvents.Add(ev);
            deferredSaveNeeded = true;
        }

        // 2a) 標準化輸入與拆詞 via TextProcessingService
        var rawText = req.Text ?? string.Empty;
        var normalizedText = _textProcessing.Normalize(rawText);
        var tokens = _textProcessing.Tokenize(normalizedText);

        // Prefilter via IPrefilterService
        var pre = _prefilter.EvaluatePrefilter(normalizedText, tokens);
        await _dbLogger.LogAsync("Debug", "Prefilter result: ConversationId={ConversationId} ShortCircuit={ShortCircuit} Reason={Reason}", new { ConversationId = req.ConversationId, ShortCircuit = pre.ShortCircuit, Reason = pre.Reason });
        if (pre.ShortCircuit)
        {
            var emptyResp = new BotQueryResponse
            {
                ShouldReply = false,
                Route = "none",
                StateChanges = new BotQueryStateChanges
                {
                    BotEnabled = true,
                    HandoffUntil = null,
                    PendingDisambiguationIds = Array.Empty<string>(),
                    PendingDisambiguationRoute = null
                },
                LogPayload = new BotQueryLogPayload
                {
                    FaqCategory = null,
                    LlmEnabled = false,
                    NeedsHumanHandoff = false,
                    IsStaffTriggered = pre.IsStaffTriggered,
                    ContextCountBefore = 0,
                    ContextCountAfter = 0
                }
            };

            var reason = pre.Reason;

            var logEmpty = new BotMessageRoute
            {
                EventRowId = ev.EventRowId,
                ConversationId = req.ConversationId,
                SourceType = sourceType,
                LineUserId = req.UserId,
                LogEvent = "bot_query",
                Route = "none",
                Reason = reason,
                MatchedFaqId = null,
                MatchedScore = null,
                MatchedBy = null,
                FaqCategory = null,
                TopFaqIds = null,
                AliasTerm = null,
                ReplyText = null,
                LlmEnabled = false,
                NeedsHumanHandoff = false,
                IsStaffTriggered = pre.IsStaffTriggered,
                ContextCountBefore = 0,
                ContextCountAfter = 0,
                LogClass = "bot_query",
                LogGroup = "bot",
                LogPriority = "info",
                LogUseful = false,
                CreatedAt = now
            };
            var persistRouteLogs2 = (Environment.GetEnvironmentVariable("PERSIST_ROUTE_LOGS") ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);
            if (persistRouteLogs2)
            {
                _db.BotMessageRoutes.Add(logEmpty);
                deferredSaveNeeded = true;
            }
            await WriteAppLogAsync("Information", "Prefilter short-circuit: ConversationId={ConversationId} Reason={Reason}", new { ConversationId = req.ConversationId, Reason = pre.Reason });

            if (deferredSaveNeeded)
            {
                try { await _db.SaveChangesAsync(); }
                catch { /* swallow to avoid affecting response */ }
            }

            return Ok(emptyResp);
        }

        // 2) 讀取會話狀態（handoff 中則不再回覆）
        var useMemoryState = (Environment.GetEnvironmentVariable("USE_INMEMORY_STATE") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);
        var stateCacheKey = $"state:{sourceType}:{req.ConversationId}";
        BotConversationState? state = await _stateService.GetStateAsync(sourceType, req.ConversationId, useMemoryState);
        await WriteAppLogAsync("Debug", "State loaded: ConversationId={ConversationId} HasState={HasState} HandoffUntil={Handoff}", new { ConversationId = req.ConversationId, HasState = state != null, Handoff = state?.HandoffUntil });
        var botEnabled = true;
        DateTimeOffset? handoffUntil = null;
        if (state != null && state.HandoffUntil.HasValue && state.HandoffUntil.Value > now)
        {
            botEnabled = false;
            handoffUntil = state.HandoffUntil;

            var respHandoff = new BotQueryResponse
            {
                ShouldReply = false,
                Route = "handoff",
                StateChanges = new BotQueryStateChanges
                {
                    BotEnabled = false,
                    HandoffUntil = handoffUntil,
                    PendingDisambiguationIds = Array.Empty<string>(),
                    PendingDisambiguationRoute = null
                },
                LogPayload = new BotQueryLogPayload
                {
                    FaqCategory = null,
                    LlmEnabled = false,
                    NeedsHumanHandoff = true,
                    IsStaffTriggered = false,
                    ContextCountBefore = 0,
                    ContextCountAfter = 0
                }
            };

            // 紀錄 route log
            var logHandoff = new BotMessageRoute
            {
                EventRowId = ev.EventRowId,
                ConversationId = req.ConversationId,
                SourceType = sourceType,
                LineUserId = req.UserId,
                LogEvent = "bot_query",
                Route = "handoff",
                Reason = "handoff_active",
                MatchedFaqId = null,
                MatchedScore = null,
                MatchedBy = "state",
                FaqCategory = null,
                TopFaqIds = null,
                AliasTerm = null,
                ReplyText = null,
                LlmEnabled = false,
                NeedsHumanHandoff = true,
                IsStaffTriggered = false,
                ContextCountBefore = 0,
                ContextCountAfter = 0,
                LogClass = "bot_query",
                LogGroup = "bot",
                LogPriority = "info",
                LogUseful = true,
                CreatedAt = now
            };
            var persistRouteLogs = (Environment.GetEnvironmentVariable("PERSIST_ROUTE_LOGS") ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);
            if (persistRouteLogs)
            {
                _db.BotMessageRoutes.Add(logHandoff);
                deferredSaveNeeded = true;
            }

            return Ok(respHandoff);
        }

        if (string.IsNullOrWhiteSpace(req.Text))
        {
            var emptyResp = new BotQueryResponse
            {
                ShouldReply = false,
                Route = "none",
                StateChanges = new BotQueryStateChanges
                {
                    BotEnabled = botEnabled,
                    HandoffUntil = handoffUntil,
                    PendingDisambiguationIds = Array.Empty<string>(),
                    PendingDisambiguationRoute = null
                },
                LogPayload = new BotQueryLogPayload
                {
                    FaqCategory = null,
                    LlmEnabled = false,
                    NeedsHumanHandoff = false,
                    IsStaffTriggered = false,
                    ContextCountBefore = 0,
                    ContextCountAfter = 0
                }
            };

            var logEmpty = new BotMessageRoute
            {
                EventRowId = ev.EventRowId,
                ConversationId = req.ConversationId,
                SourceType = sourceType,
                LineUserId = req.UserId,
                LogEvent = "bot_query",
                Route = "none",
                Reason = "empty_text",
                MatchedFaqId = null,
                MatchedScore = null,
                MatchedBy = null,
                FaqCategory = null,
                TopFaqIds = null,
                AliasTerm = null,
                ReplyText = null,
                LlmEnabled = false,
                NeedsHumanHandoff = false,
                IsStaffTriggered = false,
                ContextCountBefore = 0,
                ContextCountAfter = 0,
                LogClass = "bot_query",
                LogGroup = "bot",
                LogPriority = "info",
                LogUseful = false,
                CreatedAt = now
            };
            var persistRouteLogs2 = (Environment.GetEnvironmentVariable("PERSIST_ROUTE_LOGS") ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);
            if (persistRouteLogs2)
            {
                _db.BotMessageRoutes.Add(logEmpty);
                deferredSaveNeeded = true;
            }

            return Ok(emptyResp);
        }

        // 3) 初始化決策變數
        string route = "none";
        string? matchedFaqId = null;
        string? matchedBy = null;
        double? confidence = null;
        string? replyText = null;
        string? faqCategory = null;
        var topFaqIds = new List<string>();
        string? aliasTerm = null;
        bool needsHumanHandoff = false;
        var contextBefore = 0;
        var contextAfter = 0;

        // 3a) 處理 disambiguation 的數字選擇（委派至 service）
        try
        {
            var disRes = await _disambiguationService.TryHandleNumericSelectionAsync(state, normalizedText, sourceType, req.ConversationId, now, useMemoryState);
            if (disRes.Handled)
            {
                route = disRes.Route ?? route;
                matchedFaqId = disRes.MatchedFaqId;
                matchedBy = disRes.MatchedBy;
                confidence = disRes.Confidence;
                replyText = disRes.ReplyText;
                faqCategory = disRes.FaqCategory;
                needsHumanHandoff = disRes.NeedsHumanHandoff;
            }
        }
        catch (Exception ex)
        {
            await WriteAppLogAsync("Warning", "Disambiguation handling failed", null, ex);
        }
        if (route != "none")
        {
            await WriteAppLogAsync("Information", "Disambiguation handled: ConversationId={ConversationId} Route={Route} MatchedFaqId={FaqId}", new { ConversationId = req.ConversationId, Route = route, MatchedFaqId = matchedFaqId });
        }

        // 4) 先做 FAQ 問題精準比對（使用 SearchTextCache 嘗試避免載入所有 FAQ）
        if (route == "none")
        {
            // try exact match using precomputed SearchTextCache
            var exactFaqs = await _db.BotFaqItems
                .AsNoTracking()
                .Where(f => f.Enabled && f.SearchTextCache != null && f.SearchTextCache == normalizedText)
                .ToListAsync();

            if (exactFaqs.Count > 0)
            {
                var f = exactFaqs[0];
                route = "faq";
                matchedFaqId = f.FaqId;
                matchedBy = "faq_exact";
                confidence = 1.0;
                replyText = f.Answer;
                faqCategory = f.CategoryKey ?? f.Category;
                needsHumanHandoff = f.NeedsHumanHandoff;
                await WriteAppLogAsync("Information", "Exact FAQ match: ConversationId={ConversationId} FaqId={FaqId}", new { ConversationId = req.ConversationId, FaqId = matchedFaqId });
            }

            // fuzzy/synonym: use pg_trgm trigram similarity in DB to limit candidates
            if (route == "none")
            {
                // Let Postgres use the trigram index and similarity operator to return best candidates
                                var trigramCandidates = await _db.BotFaqItems
                                        .FromSqlInterpolated($@"SELECT * FROM bot_faq_items
                                                WHERE ""Enabled"" = true AND ""SearchTextCache"" IS NOT NULL
                                                    AND ""SearchTextCache"" % {normalizedText}
                                                ORDER BY similarity(""SearchTextCache"", {normalizedText}) DESC
                                                LIMIT 500")
                                        .AsNoTracking()
                                        .ToListAsync();

                foreach (var f in trigramCandidates)
                {
                    var qSearchCache = f.SearchTextCache ?? string.Empty;
                    var overlap = _textProcessing.TokenOverlapScore(normalizedText, qSearchCache);
                    if (overlap >= 0.95)
                    {
                        route = "faq";
                        matchedFaqId = f.FaqId;
                        matchedBy = "faq_synonym";
                        confidence = 1.0;
                        replyText = f.Answer;
                        faqCategory = f.CategoryKey ?? f.Category;
                        needsHumanHandoff = f.NeedsHumanHandoff;
                        await WriteAppLogAsync("Information", "High-overlap faq matched: ConversationId={ConversationId} FaqId={FaqId} Overlap={Overlap}", new { ConversationId = req.ConversationId, FaqId = f.FaqId, Overlap = overlap });
                        break;
                    }
                }
            }
        }

        // 5) 若尚未決策，做 alias 精準比對（使用 normalized 比對）
        if (route == "none")
        {
            var am = await _aliasService.MatchAliasAsync(normalizedText);
            if (am != null)
            {
                await WriteAppLogAsync("Information", "Alias matched: ConversationId={ConversationId} AliasTerm={Alias} Mode={Mode} FaqIds={FaqIds}", new { ConversationId = req.ConversationId, AliasTerm = am.AliasTerm, Mode = am.Mode, FaqIds = am.FaqIds });
                aliasTerm = am.AliasTerm;
                var aliasFaqIds = am.FaqIds ?? Array.Empty<string>();
                var mode = am.Mode ?? string.Empty;
                if (mode == "direct" && aliasFaqIds.Length == 1)
                {
                    matchedFaqId = aliasFaqIds[0];
                    matchedBy = "alias_direct";
                    confidence = 1.0;
                    route = "faq";
                    faqCategory = null;

                    var faq = await _faqService.FindByIdsAsync(new[] { matchedFaqId });
                    var f = faq.FirstOrDefault();
                    if (f != null)
                    {
                        replyText = f.Answer;
                        faqCategory = f.CategoryKey ?? f.Category;
                        needsHumanHandoff = f.NeedsHumanHandoff;
                    }
                }
                else if (mode == "preferred" && aliasFaqIds.Length > 0)
                {
                    var enabled = await _faqService.FindByIdsAsync(aliasFaqIds);
                    if (enabled.Count > 0)
                    {
                        var chosen = enabled[0];
                        matchedFaqId = chosen.FaqId;
                        matchedBy = "alias_preferred";
                        confidence = 0.95;
                        route = "faq";
                        replyText = chosen.Answer;
                        faqCategory = chosen.CategoryKey ?? chosen.Category;
                        needsHumanHandoff = chosen.NeedsHumanHandoff;
                    }
                    else
                    {
                        route = "candidates";
                        matchedBy = "alias_disambiguation";
                        topFaqIds.AddRange(aliasFaqIds);
                    }
                }
                else if (aliasFaqIds.Length > 0)
                {
                    route = "candidates";
                    matchedBy = "alias_disambiguation";
                    topFaqIds.AddRange(aliasFaqIds);
                }
            }
        }

        // 6) 若尚未決策，改用 Embedding 搜尋
        if (route == "none")
        {
            const double defaultDirectLow = 0.44;
            const double defaultCosineWeight = 0.7;
            const double defaultOverlapWeight = 0.3;

            // Cache BotConstantsConfigs to avoid DB hit on every request
            var cacheKeySettings = "BotConstantsConfigs";
            List<ARCompletions.Domain.BotConstantsConfig>? settings = null;
            if (!_cache.TryGetValue(cacheKeySettings, out settings))
            {
                settings = await _db.BotConstantsConfigs
                    .AsNoTracking()
                    .ToListAsync();
                var cacheSecsStr = Environment.GetEnvironmentVariable("BOT_CONFIG_CACHE_SECONDS") ?? "60";
                if (!int.TryParse(cacheSecsStr, out var cacheSecs)) cacheSecs = 60;
                var cacheEntryOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(cacheSecs));
                _cache.Set(cacheKeySettings, settings, cacheEntryOptions);
            }
            settings ??= new List<ARCompletions.Domain.BotConstantsConfig>();

            double GetDouble(string key, double def)
            {
                var cfg = settings.FirstOrDefault(c => c.ConfigKey == key);
                if (cfg?.ConfigValue == null) return def;
                return double.TryParse(cfg.ConfigValue, out var v) ? v : def;
            }

            bool GetBool(string key, bool def)
            {
                var cfg = settings.FirstOrDefault(c => c.ConfigKey == key);
                if (cfg?.ConfigValue == null) return def;
                return bool.TryParse(cfg.ConfigValue, out var v) ? v : def;
            }

            string GetString(string key, string def)
            {
                var cfg = settings.FirstOrDefault(c => c.ConfigKey == key);
                return string.IsNullOrWhiteSpace(cfg?.ConfigValue) ? def : cfg!.ConfigValue!;
            }

            var directLow = GetDouble("bot.embedding.directLow", defaultDirectLow);
            var cosineWeight = GetDouble("bot.embedding.cosineWeight", defaultCosineWeight);
            var overlapWeight = GetDouble("bot.embedding.overlapWeight", defaultOverlapWeight);
            var allowDirect = GetBool("bot.embedding.allowDirect", true);

            // 儲存向量所使用的 provider / model，可透過 bot_constants_config 切換
            var embeddingProvider = GetString("bot.embedding.provider", "local_hash");
            var modelName = GetString("bot.embedding.model", "text-embedding-3-small");

            double[]? queryVec = null;
            try
            {
                queryVec = await _embeddingRetrieval.GetOrCreateEmbeddingAsync(normalizedText, modelName);
                await WriteAppLogAsync("Debug", "Embedding retrieved: ConversationId={ConversationId} VecLen={Len}", new { ConversationId = req.ConversationId, VecLen = queryVec?.Length ?? 0 });
            }
            catch (Exception ex)
            {
                await WriteAppLogAsync("Warning", "Embedding retrieval failed for text", new { Text = normalizedText }, ex);
                queryVec = null;
            }

            // If embedding not available, use keyword/token-overlap fallback to produce candidates
            if (queryVec == null)
            {
                // use SearchTextCache for fallback and limit scan size
                var faqsForFallback = await _db.BotFaqItems.AsNoTracking()
                    .Where(f => f.Enabled && f.SearchTextCache != null)
                    .Take(500)
                    .ToListAsync();
                var fallbackScores = new Dictionary<string, double>();
                foreach (var f in faqsForFallback)
                {
                    var qSearchCache = f.SearchTextCache ?? string.Empty;
                    var ov = _textProcessing.TokenOverlapScore(normalizedText, qSearchCache);
                    if (ov > 0)
                    {
                        fallbackScores[f.FaqId] = ov;
                    }
                }

                if (fallbackScores.Count > 0)
                {
                    var rankedFb = fallbackScores.OrderByDescending(kv => kv.Value).ToList();
                    topFaqIds = rankedFb.Select(kv => kv.Key).Take(5).ToList();
                    var bestFb = rankedFb[0];
                    matchedFaqId = bestFb.Key;
                    confidence = bestFb.Value;
                    await WriteAppLogAsync("Information", "Keyword fallback ranked: ConversationId={ConversationId} TopIds={TopIds}", new { ConversationId = req.ConversationId, TopIds = topFaqIds });

                    var bestFaq = faqsForFallback.FirstOrDefault(f => f.FaqId == matchedFaqId);
                    var minScore = bestFaq?.MinConfidenceScore ?? directLow;
                    if (allowDirect && confidence >= minScore)
                    {
                        route = "faq";
                        matchedBy = "keyword_fallback";
                        replyText = bestFaq?.Answer;
                        faqCategory = bestFaq?.CategoryKey ?? bestFaq?.Category;
                        needsHumanHandoff = bestFaq?.NeedsHumanHandoff ?? false;
                    }
                    else
                    {
                        route = "candidates";
                        matchedBy = "keyword_fallback";
                    }
                }
            }

            if (queryVec != null && queryVec.Length > 0)
            {
                var isComposite = _textProcessing.IsComposite(normalizedText, tokens);

                // Build top candidate IDs via CandidateBuilderService
                List<string>? built = null;
                try
                {
                    built = await _candidateBuilder.BuildCandidatesAsync(normalizedText, queryVec, embeddingProvider, 5);
                }
                catch (Exception ex)
                {
                    await WriteAppLogAsync("Warning", "CandidateBuilder.BuildCandidatesAsync failed", new { Text = normalizedText }, ex);
                    built = null;
                }

                if (built != null && built.Count > 0)
                {
                    topFaqIds = built;
                    await WriteAppLogAsync("Information", "Candidates built: ConversationId={ConversationId} Ids={Ids}", new { ConversationId = req.ConversationId, Ids = topFaqIds });

                    // fetch details for scoring/decision
                    List<ARCompletions.Domain.BotFaqItem> faqs;
                    try
                    {
                        faqs = await _faqService.FindByIdsAsync(topFaqIds);
                    }
                    catch (Exception ex)
                    {
                        await WriteAppLogAsync("Warning", "FaqService.FindByIdsAsync failed", new { Ids = topFaqIds }, ex);
                        faqs = new List<ARCompletions.Domain.BotFaqItem>();
                    }
                    await WriteAppLogAsync("Debug", "Fetched FAQ details: ConversationId={ConversationId} Count={Count}", new { ConversationId = req.ConversationId, Count = faqs.Count });
                    var faqMap = faqs.ToDictionary(f => f.FaqId, f => f);

                    // compute scores for these candidates
                    var candidateTuples = new List<(string FaqId, double[]? Vec, string Question, string SearchTextCache)>();

                    // Batch fetch embeddings to avoid N+1 queries
                    var faqIds = faqs.Select(f => f.FaqId).ToList();
                    var embeddings = await _db.BotFaqEmbeddings.AsNoTracking()
                        .Where(e => faqIds.Contains(e.FaqId) && e.Embedding != null && e.Embedding.Length > 0 && e.EmbeddingProvider == embeddingProvider)
                        .ToListAsync();
                    var embMap = embeddings.ToDictionary(e => e.FaqId, e => e.Embedding);
                    await WriteAppLogAsync("Debug", "Batch fetched embeddings: ConversationId={ConversationId} FetchedCount={Count}", new { ConversationId = req.ConversationId, FetchedCount = embeddings.Count });

                    foreach (var f in faqs)
                    {
                        embMap.TryGetValue(f.FaqId, out var vec);
                        candidateTuples.Add((f.FaqId, vec, f.Question ?? string.Empty, f.SearchTextCache ?? string.Empty));
                    }

                    var scores = _scoring.ScoreCandidates(queryVec ?? Array.Empty<double>(), candidateTuples, normalizedText);
                    var ranked = scores.OrderByDescending(kv => kv.Value).ToList();
                    await WriteAppLogAsync("Debug", "Scoring results: ConversationId={ConversationId} Ranked={Ranked}", new { ConversationId = req.ConversationId, Ranked = string.Join(',', ranked.Select(kv => kv.Key + ":" + kv.Value)) });
                    var best = ranked.FirstOrDefault();
                    if (!string.IsNullOrEmpty(best.Key))
                    {
                        matchedFaqId = best.Key;
                        confidence = best.Value;
                        if (faqMap.TryGetValue(matchedFaqId, out var bestFaq))
                        {
                            var minScore = bestFaq.MinConfidenceScore ?? directLow;
                            if (allowDirect && confidence >= minScore && !isComposite)
                            {
                                route = "faq";
                                matchedBy = "embedding";
                                replyText = bestFaq.Answer;
                                faqCategory = bestFaq.CategoryKey ?? bestFaq.Category;
                                needsHumanHandoff = bestFaq.NeedsHumanHandoff;
                            }
                            else
                            {
                                route = "candidates";
                                matchedBy = isComposite ? "embedding_composite" : "embedding_low_conf";
                            }
                        }
                        else
                        {
                            // fallback: candidate not found in fetched details
                            route = "candidates";
                            matchedBy = isComposite ? "embedding_composite" : "embedding_low_conf";
                        }
                    }
                }
            }
        }

        // 7) 更新會話狀態（針對 disambiguation）
        if (state == null)
        {
            state = new BotConversationState
            {
                SourceType = sourceType,
                ConversationId = req.ConversationId,
                UpdatedAt = now
            };
            // defer DB commit; BotController will SaveChanges once at end
            await _stateService.SaveStateAsync(state, useMemoryState, stateCacheKey, deferSave: true);
        }

        if (route == "candidates" && topFaqIds.Count > 0)
        {
            state.PendingDisambiguationIds = JsonSerializer.Serialize(topFaqIds);
            state.PendingDisambiguationRoute = "faq";
            state.PendingDisambiguationAt = now;
            await WriteAppLogAsync("Debug", "Set pending disambiguation: ConversationId={ConversationId} Ids={Ids}", new { ConversationId = req.ConversationId, Ids = topFaqIds });
        }
        else
        {
            state.PendingDisambiguationIds = null;
            state.PendingDisambiguationRoute = null;
            state.PendingDisambiguationAt = null;
        }
        state.UpdatedAt = now;
        await _stateService.SaveStateAsync(state, useMemoryState, stateCacheKey, deferSave: true);
        await WriteAppLogAsync("Debug", "Saved state: ConversationId={ConversationId} PendingIds={PendingIds}", new { ConversationId = req.ConversationId, PendingIds = state.PendingDisambiguationIds });

        // 8) 組合回傳物件
        var shouldReply = route == "faq" || (route == "candidates" && topFaqIds.Count > 0);

        object[] quickReplies = Array.Empty<object>();
        if (route == "candidates" && topFaqIds.Count > 0)
        {
            var faqList = await _db.BotFaqItems
                .AsNoTracking()
                .Where(f => topFaqIds.Contains(f.FaqId))
                .ToListAsync();
            var faqDict = faqList.ToDictionary(f => f.FaqId, f => f);

            var qr = new List<object>();
            foreach (var id in topFaqIds)
            {
                if (!faqDict.TryGetValue(id, out var faq)) continue;
                qr.Add(new
                {
                    faqId = faq.FaqId,
                    question = faq.Question,
                    categoryKey = faq.CategoryKey
                });
            }
            quickReplies = qr.ToArray();
        }

        var resp = _responseBuilder.BuildResponse(
            route,
            matchedFaqId,
            matchedBy,
            confidence,
            replyText,
            route == "faq" ? "faq" : route,
            quickReplies,
            state,
            botEnabled,
            handoffUntil,
            faqCategory,
            needsHumanHandoff,
            contextBefore,
            contextAfter);

        // 9) 寫入 route log
        var routeLog = new BotMessageRoute
        {
            EventRowId = ev.EventRowId,
            ConversationId = req.ConversationId,
            SourceType = sourceType,
            LineUserId = req.UserId,
            LogEvent = "bot_query",
            Route = route,
            Reason = matchedBy,
            MatchedFaqId = matchedFaqId,
            MatchedScore = confidence,
            MatchedBy = matchedBy,
            FaqCategory = faqCategory,
            TopFaqIds = topFaqIds.Count > 0 ? JsonSerializer.Serialize(topFaqIds) : null,
            AliasTerm = aliasTerm,
            ReplyText = replyText,
            LlmEnabled = false,
            NeedsHumanHandoff = needsHumanHandoff,
            IsStaffTriggered = false,
            ContextCountBefore = contextBefore,
            ContextCountAfter = contextAfter,
            LogClass = "bot_query",
            LogGroup = "bot",
            LogPriority = "info",
            LogUseful = shouldReply,
            CreatedAt = now
        };
        var persistRouteLogs3 = (Environment.GetEnvironmentVariable("PERSIST_ROUTE_LOGS") ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);
        await _routeLogger.LogRouteAsync(routeLog, persistRouteLogs3);

        if (deferredSaveNeeded)
        {
            try { await _db.SaveChangesAsync(); }
            catch { /* swallow to avoid affecting response */ }
        }
        sw.Stop();
        await WriteAppLogAsync("Information", "Query finished: ConversationId={ConversationId} Route={Route} ShouldReply={ShouldReply} ElapsedMs={Ms}", new { ConversationId = req.ConversationId, Route = route, ShouldReply = shouldReply, ElapsedMs = sw.ElapsedMilliseconds });

        return Ok(resp);
    }

    // Removed: events, routes, llm-logs endpoints (handled externally or not needed)
}

public class BotQueryRequest
{
    public string SourceType { get; set; } = "group";
    public string ConversationId { get; set; } = string.Empty;
    public string? GroupId { get; set; }
    public string? RoomId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? ReplyToken { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; }
    public string? RawEvent { get; set; }
}

public class BotQueryResponse
{
    public bool ShouldReply { get; set; }
    public string? Route { get; set; }
    public string? MatchedFaqId { get; set; }
    public string? MatchedBy { get; set; }
    public double? Confidence { get; set; }
    public string? ReplyText { get; set; }
    public string? ReplyMode { get; set; }
    public object[] QuickReplyItems { get; set; } = Array.Empty<object>();
    public BotQueryStateChanges StateChanges { get; set; } = new();
    public BotQueryLogPayload LogPayload { get; set; } = new();
}

public class BotQueryStateChanges
{
    public bool BotEnabled { get; set; }
    public DateTimeOffset? HandoffUntil { get; set; }
    public string[] PendingDisambiguationIds { get; set; } = Array.Empty<string>();
    public string? PendingDisambiguationRoute { get; set; }
}

public class BotQueryLogPayload
{
    public string? FaqCategory { get; set; }
    public bool LlmEnabled { get; set; }
    public bool NeedsHumanHandoff { get; set; }
    public bool IsStaffTriggered { get; set; }
    public int ContextCountBefore { get; set; }
    public int ContextCountAfter { get; set; }
}

// Removed DTOs: BotEventCreateRequest, BotRouteCreateRequest, BotLlmLogCreateRequest
