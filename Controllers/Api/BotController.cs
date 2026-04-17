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
    private readonly Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> _dbFactory;
    private readonly IMemoryCache _cache;
    private readonly IBotConstantsService _botConstants;
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
        Microsoft.EntityFrameworkCore.IDbContextFactory<ARCompletionsContext> dbFactory,
        IMemoryCache cache,
        IBotConstantsService botConstants,
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
        _dbFactory = dbFactory;
        _cache = cache;
        _botConstants = botConstants;
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

    private async Task WriteAppLogAsync(string level, string message, object? props = null, Exception? ex = null, ARCompletionsContext? db = null)
    {
        try
        {
            if (_dbLogger != null)
            {
                // Default to deferred save to reduce DB roundtrips. Only force immediate
                // save for critical end-of-request logs or exceptions.
                var deferSave = true;
                if (ex != null) deferSave = false;
                // keep "Query finished" immediate so end-to-end latency is recorded
                if (!string.IsNullOrEmpty(message) && message.Contains("Query finished")) deferSave = false;

                if (db != null)
                {
                    // use caller's context; saveNow depends on deferSave flag
                    await _dbLogger.LogAsync(db, level, message, props, ex, saveNow: !deferSave);
                }
                else
                {
                    // immediate write using DbLogger's own context
                    await _dbLogger.LogAsync(level, message, props, ex, deferSave: false);
                }
            }
        }
        catch
        {
            // swallow to avoid interfering with request flow
        }
    }

    // Fetch the latest (most recently rebuilt) embedding row per FaqId for a given provider.
    // Uses DISTINCT ON(...) to let Postgres return one row per FaqId ordered by RebuiltAt DESC.
    private async Task<List<ARCompletions.Domain.BotFaqEmbedding>> FetchLatestEmbeddingsAsync(List<string> faqIds, string provider)
    {
        if (faqIds == null || faqIds.Count == 0) return new List<ARCompletions.Domain.BotFaqEmbedding>();

        // Use UNNEST + LATERAL to fetch one latest row per FaqId in a single query.
        // This pattern lets Postgres use an index on (FaqId, EmbeddingProvider, IsActive, RebuiltAt)
        // to satisfy the LIMIT 1 per FaqId efficiently. Add timing logs to measure DB fetch cost.
        var fetchStart = DateTime.UtcNow;

        // create a local db context early so logs can be batched with other DB changes
        using var db = _dbFactory.CreateDbContext();
        try
        {
            await WriteAppLogAsync("Debug", "FetchLatestEmbeddingsAsync START", new { Count = faqIds.Count, Provider = provider }, null, db);
        }
        catch { }

        // Allow switching to the materialized-view path for fastest lookups.
        var useMatview = (Environment.GetEnvironmentVariable("USE_LATEST_MATVIEW") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);
        List<ARCompletions.Domain.BotFaqEmbedding> res;
        if (useMatview)
        {
            // latest_bot_faq_embeddings contains one row per (FaqId, EmbeddingProvider)
            res = await db.BotFaqEmbeddings
                .FromSqlInterpolated($@"
                    SELECT l.* FROM unnest({faqIds}) AS f(faqid)
                    LEFT JOIN latest_bot_faq_embeddings l
                      ON l.""FaqId"" = f.faqid
                     AND l.""EmbeddingProvider"" = {provider};")
                .AsNoTracking()
                .ToListAsync();
        }
        else
        {
            res = await db.BotFaqEmbeddings
                .FromSqlInterpolated($@"
                    SELECT e.* FROM unnest({faqIds}) AS f(faqid)
                    LEFT JOIN LATERAL (
                        SELECT e2.* FROM bot_faq_embeddings e2
                        WHERE e2.""FaqId"" = f.faqid
                            AND e2.""EmbeddingProvider"" = {provider}
                            AND e2.""Embedding"" IS NOT NULL
                            AND e2.""IsActive"" = TRUE
                        ORDER BY e2.""RebuiltAt"" DESC
                        LIMIT 1
                    ) e ON true;")
                .AsNoTracking()
                .ToListAsync();
        }

        var fetchMs = (DateTime.UtcNow - fetchStart).TotalMilliseconds;
        try
        {
            await WriteAppLogAsync("Debug", "FetchLatestEmbeddingsAsync END", new { Fetched = res?.Count ?? 0, ElapsedMs = fetchMs, Provider = provider }, null, db);
        }
        catch { }

        return res ?? new List<ARCompletions.Domain.BotFaqEmbedding>();
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
        using var db = _dbFactory.CreateDbContext();
        await WriteAppLogAsync("Information", "Query received: ConversationId={ConversationId} UserId={UserId} SourceType={SourceType} TextLen={TextLen}", new { ConversationId = req.ConversationId, UserId = req.UserId, SourceType = req.SourceType, TextLen = (req.Text ?? string.Empty).Length }, null, db);

        var now = DateTimeOffset.UtcNow;
        // Load bot constants early so numeric limits can be used throughout the request
        var _allBotConfigs = await _botConstants.GetAllConfigsAsync().ConfigureAwait(false);
        _allBotConfigs ??= new List<ARCompletions.Domain.BotConstantsConfig>();
        int GetIntConfig(string key, int def)
        {
            var cfg = _allBotConfigs.FirstOrDefault(c => c.ConfigKey == key);
            if (cfg?.ConfigValue == null) return def;
            return int.TryParse(cfg.ConfigValue, out var v) ? v : def;
        }
        var sourceType = string.IsNullOrWhiteSpace(req.SourceType) ? "group" : req.SourceType;

        // 可配置是否強制轉成 UTC（預設 true）
        var forceUtc = (Environment.GetEnvironmentVariable("FORCE_UTC") ?? "true").Equals("true", StringComparison.OrdinalIgnoreCase);
        var receivedAt = req.ReceivedAt == default
            ? now
            : (forceUtc ? req.ReceivedAt.ToUniversalTime() : req.ReceivedAt);

        // 1) 寫入 incoming event（可選擇 persist）
        var ev = new BotIncomingEvent
        {
            // Store the textual user input so UI and analysis can use it.
            Text = req.Text,
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
            db.BotIncomingEvents.Add(ev);
            deferredSaveNeeded = true;
        }

        // 2a) 標準化輸入與拆詞 via TextProcessingService
        var rawText = req.Text ?? string.Empty;
        var normalizedText = _textProcessing.Normalize(rawText);
        var tokens = _textProcessing.Tokenize(normalizedText);

        // Prefilter via IPrefilterService
        var pre = _prefilter.EvaluatePrefilter(normalizedText, tokens);
        await WriteAppLogAsync("Debug", "Prefilter result: ConversationId={ConversationId} ShortCircuit={ShortCircuit} Reason={Reason}", new { ConversationId = req.ConversationId, ShortCircuit = pre.ShortCircuit, Reason = pre.Reason }, null, db);

        // Additional debug information: record normalized text and token summary to help diagnose short-circuits
        try
        {
            await WriteAppLogAsync("Debug", "Prefilter debug: ConversationId={ConversationId} Reason={Reason} Text={Text} Tokens={Tokens}",
                new { ConversationId = req.ConversationId, Reason = pre.Reason, Text = normalizedText, Tokens = string.Join(' ', tokens ?? Array.Empty<string>()) }, null, db);
        }
        catch
        {
            // swallow logging errors to avoid affecting request flow
        }
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
                db.BotMessageRoutes.Add(logEmpty);
                deferredSaveNeeded = true;
            }
            await WriteAppLogAsync("Information", "Prefilter short-circuit: ConversationId={ConversationId} Reason={Reason}", new { ConversationId = req.ConversationId, Reason = pre.Reason }, null, db);

            if (deferredSaveNeeded)
            {
                try { await db.SaveChangesAsync(); }
                catch { /* swallow to avoid affecting response */ }
            }

            return Ok(emptyResp);
        }

        // 2) 讀取會話狀態（handoff 中則不再回覆）
        var useMemoryState = (Environment.GetEnvironmentVariable("USE_INMEMORY_STATE") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);
        var stateCacheKey = $"state:{sourceType}:{req.ConversationId}";
        BotConversationState? state = await _stateService.GetStateAsync(sourceType, req.ConversationId, useMemoryState, db);
        await WriteAppLogAsync("Debug", "State loaded: ConversationId={ConversationId} HasState={HasState} HandoffUntil={Handoff}", new { ConversationId = req.ConversationId, HasState = state != null, Handoff = state?.HandoffUntil }, null, db);
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
                db.BotMessageRoutes.Add(logHandoff);
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
                db.BotMessageRoutes.Add(logEmpty);
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
            var disRes = await _disambiguationService.TryHandleNumericSelectionAsync(state, normalizedText, sourceType, req.ConversationId, now, useMemoryState, db);
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
            await WriteAppLogAsync("Warning", "Disambiguation handling failed", null, ex, db);
        }
        if (route != "none")
        {
            await WriteAppLogAsync("Information", "Disambiguation handled: ConversationId={ConversationId} Route={Route} MatchedFaqId={FaqId}", new { ConversationId = req.ConversationId, Route = route, MatchedFaqId = matchedFaqId }, null, db);
        }

        // 4) 先做 FAQ 問題精準比對（使用 SearchTextCache 嘗試避免載入所有 FAQ）
        if (route == "none")
        {
            // try exact match using precomputed SearchTextCache
            var exactFaqs = await db.BotFaqItems
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
                await WriteAppLogAsync("Information", "Exact FAQ match: ConversationId={ConversationId} FaqId={FaqId}", new { ConversationId = req.ConversationId, FaqId = matchedFaqId }, null, db);
            }

            // fuzzy/synonym: use pg_trgm trigram similarity in DB to limit candidates
            if (route == "none")
            {
                // Let Postgres use the trigram index and similarity operator to return best candidates
                // Use a two-step query: first select only keys (small rows) to encourage index use,
                // then join back to fetch SearchTextCache and order by similarity.
                                var trigramMinSimStr = Environment.GetEnvironmentVariable("TRIGRAM_MIN_SIMILARITY") ?? "0.1";
                                if (!double.TryParse(trigramMinSimStr, out var trigramMinSim)) trigramMinSim = 0.1;

                                // Use a materialized candidates view to make trigram selection index-friendly.
                                // The materialized view `bot_faq_items_candidates` is a lightweight subset (enabled rows with SearchTextCache).
                                // Select ids from the materialized view using the % operator (GIN trigram index), then join back to main table.
                                var trigramCandidates = await db.BotFaqItems
                                    .FromSqlInterpolated($@"SELECT i.*
                                        FROM bot_faq_items i
                                        JOIN (
                                            SELECT m.""FaqId"", m.""SearchTextCache"" FROM bot_faq_items_candidates m
                                            WHERE m.""SearchTextCache"" % {normalizedText}
                                            LIMIT 500
                                        ) c ON c.""FaqId"" = i.""FaqId""
                                        ORDER BY similarity(c.""SearchTextCache"", {normalizedText}) DESC
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
                        await WriteAppLogAsync("Information", "High-overlap faq matched: ConversationId={ConversationId} FaqId={FaqId} Overlap={Overlap}", new { ConversationId = req.ConversationId, FaqId = f.FaqId, Overlap = overlap }, null, db);
                        break;
                    }
                }
            }
        }

        // 5) 若尚未決策，做 alias 精準比對（使用 normalized 比對）
        if (route == "none")
        {
            var am = await _aliasService.MatchAliasAsync(normalizedText, db);
            if (am != null)
            {
                await WriteAppLogAsync("Information", "Alias matched: ConversationId={ConversationId} AliasTerm={Alias} Mode={Mode} FaqIds={FaqIds}", new { ConversationId = req.ConversationId, AliasTerm = am.AliasTerm, Mode = am.Mode, FaqIds = am.FaqIds }, null, db);
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

                    var faq = await _faqService.FindByIdsAsync(new[] { matchedFaqId }, db);
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
                    var enabled = await _faqService.FindByIdsAsync(aliasFaqIds, db);
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
            const double defaultDirectLow = 0.012;
            const double defaultCosineWeight = 0.7;
            const double defaultOverlapWeight = 0.3;

            // Load bot constants via IBotConstantsService (service caches internally)
            var settings = await _botConstants.GetAllConfigsAsync().ConfigureAwait(false);
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
                // Try local_hash first and use thresholds to decide whether to call provider
                var syncTimeoutMs = int.TryParse(Environment.GetEnvironmentVariable("EMBEDDING_SYNC_TIMEOUT_MS") ?? "1000", out var t) ? t : 1000;
                using var cts = new System.Threading.CancellationTokenSource(syncTimeoutMs);

                // thresholds (can be configured via bot_constants_configs)
                // Prefer explicitly named confidence keys; fall back to legacy keys if absent
                var HIGH = GetDouble("bot.embedding.highConfidence", GetDouble("bot.embedding.high", 0.70));
                var MIN = GetDouble("bot.embedding.minConfidence", GetDouble("bot.embedding.min", 0.44));

                // 1) compute local embedding (measure time)
                var localEmbStart = DateTime.UtcNow;
                var localVec = await _embeddingRetrieval.GetOrCreateEmbeddingAsync(normalizedText, modelName, "local_hash", cts.Token, db);
                var localEmbMs = (DateTime.UtcNow - localEmbStart).TotalMilliseconds;
                await WriteAppLogAsync("Debug", "Local embedding retrieved: ConversationId={ConversationId} Len={Len} ElapsedMs={ElapsedMs}", new { ConversationId = req.ConversationId, Len = localVec?.Length ?? 0, ElapsedMs = localEmbMs }, null, db);

                List<string> initialCandidates = new();
                double localBestScore = 0.0;

                if (localVec != null && localVec.Length > 0)
                {
                    // build candidates using local vector
                    try
                    {
                        var buildStart = DateTime.UtcNow;
                        await WriteAppLogAsync("Debug", "Controller: BuildCandidatesAsync START", new { ConversationId = req.ConversationId, Phase = "local_prebuild" }, null, db);
                        var built = await _candidateBuilder.BuildCandidatesAsync(normalizedText, localVec, "local_hash", GetIntConfig("bot.faq.topCandidates", 5), db);
                        var buildMs = (DateTime.UtcNow - buildStart).TotalMilliseconds;
                        await WriteAppLogAsync("Debug", "Controller: BuildCandidatesAsync END", new { ConversationId = req.ConversationId, Phase = "local_postbuild", ElapsedMs = buildMs, CandidateCount = built?.Count ?? 0 }, null, db);
                        if (built != null && built.Count > 0)
                        {
                            initialCandidates = built;

                            // fetch faq details and embeddings in parallel to reduce roundtrips
                            await WriteAppLogAsync("Debug", "Controller: FetchFaqsAndEmbeddings START", new { ConversationId = req.ConversationId, Provider = "local_hash", Count = initialCandidates.Count }, null, db);
                            var faqTask = _faqService.FindByIdsAsync(initialCandidates, db);
                            var embTask = FetchLatestEmbeddingsAsync(initialCandidates, "local_hash");
                            await Task.WhenAll(faqTask, embTask);
                            var faqList = faqTask.Result;
                            var embeddings = embTask.Result ?? new List<ARCompletions.Domain.BotFaqEmbedding>();
                            await WriteAppLogAsync("Debug", "Controller: FetchFaqsAndEmbeddings END", new { ConversationId = req.ConversationId, Provider = "local_hash", Fetched = embeddings.Count, Faqlen = faqList.Count }, null, db);
                            var faqDict = faqList.ToDictionary(f => f.FaqId, f => f);

                            var embMap = embeddings.ToDictionary(e => e.FaqId, e => e.Embedding);

                            var candidateTuples = new List<(string FaqId, double[]? Vec, string Question, string SearchTextCache)>();
                            foreach (var f in faqList)
                            {
                                embMap.TryGetValue(f.FaqId, out var vec);
                                candidateTuples.Add((f.FaqId, vec, f.Question ?? string.Empty, f.SearchTextCache ?? string.Empty));
                            }

                            var scoreStart = DateTime.UtcNow;
                            await WriteAppLogAsync("Debug", "Controller: Scoring START", new { ConversationId = req.ConversationId, CandidateCount = candidateTuples.Count, Mode = "local" }, null, db);
                            var scores = _scoring.ScoreCandidates(localVec, candidateTuples, normalizedText, faqDict);
                            var scoreMs = (DateTime.UtcNow - scoreStart).TotalMilliseconds;
                            await WriteAppLogAsync("Debug", "Controller: Scoring END", new { ConversationId = req.ConversationId, CandidateCount = candidateTuples.Count, Mode = "local", ElapsedMs = scoreMs }, null, db);
                            var filtered = scores.Where(kv => kv.Value >= 0.0001).ToDictionary(kv => kv.Key, kv => kv.Value);
                            var ranked = filtered.OrderByDescending(kv => kv.Value).ToList();
                            if (ranked.Count == 0 && scores.Count > 0) ranked = scores.OrderByDescending(kv => kv.Value).ToList();

                            if (ranked.Count > 0)
                            {
                                localBestScore = ranked[0].Value;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await WriteAppLogAsync("Warning", "Local candidate building/scoring failed", new { ConversationId = req.ConversationId }, ex, db);
                    }
                }

                // Decision: use local if strong enough; otherwise call provider for rerank
                if (localBestScore >= HIGH)
                {
                    // treat as direct match using local space
                    // set queryVec to localVec and let existing scoring flow handle selection using local provider
                    queryVec = localVec;
                    embeddingProvider = "local_hash";
                    await WriteAppLogAsync("Information", "Local best exceeds HIGH; using local result", new { ConversationId = req.ConversationId, Score = localBestScore }, null, db);
                }
                else if (localBestScore >= MIN)
                {
                    // medium confidence — prefer disambiguation/candidates (do not immediately call provider)
                    queryVec = localVec;
                    embeddingProvider = "local_hash";
                    await WriteAppLogAsync("Information", "Local best in [MIN, HIGH); returning candidates", new { ConversationId = req.ConversationId, Score = localBestScore }, null, db);
                }
                else
                {
                    // low confidence: attempt provider embedding and rerank among initialCandidates (or fall back)
                    double[]? providerVec = null;
                    try
                    {
                        providerVec = await _embeddingRetrieval.GetOrCreateEmbeddingAsync(normalizedText, modelName, embeddingProvider, cts.Token, db);
                        await WriteAppLogAsync("Debug", "Provider embedding retrieved: ConversationId={ConversationId} Provider={Provider} Len={Len}", new { ConversationId = req.ConversationId, Provider = embeddingProvider, Len = providerVec?.Length ?? 0 }, null, db);
                    }
                    catch (Exception ex)
                    {
                        await WriteAppLogAsync("Warning", "Provider embedding retrieval failed", new { ConversationId = req.ConversationId, Provider = embeddingProvider }, ex, db);
                        providerVec = null;
                    }

                    if (providerVec != null && providerVec.Length > 0 && initialCandidates.Count > 0)
                    {
                        // fetch provider embeddings for same candidates
                        // fetch faq details and provider embeddings in parallel
                        await WriteAppLogAsync("Debug", "Controller: FetchFaqsAndEmbeddings START", new { ConversationId = req.ConversationId, Provider = embeddingProvider, Count = initialCandidates.Count }, null, db);
                        var faqTask2 = _faqService.FindByIdsAsync(initialCandidates, db);
                        var embTask2 = FetchLatestEmbeddingsAsync(initialCandidates, embeddingProvider);
                        await Task.WhenAll(faqTask2, embTask2);
                        var faqList = faqTask2.Result;
                        var embeddings = embTask2.Result ?? new List<ARCompletions.Domain.BotFaqEmbedding>();
                        await WriteAppLogAsync("Debug", "Controller: FetchFaqsAndEmbeddings END", new { ConversationId = req.ConversationId, Provider = embeddingProvider, Fetched = embeddings.Count, Faqlen = faqList.Count }, null, db);
                        var faqDict = faqList.ToDictionary(f => f.FaqId, f => f);

                        var embMap = embeddings.ToDictionary(e => e.FaqId, e => e.Embedding);

                        var candidateTuples = new List<(string FaqId, double[]? Vec, string Question, string SearchTextCache)>();
                        foreach (var f in faqList)
                        {
                            embMap.TryGetValue(f.FaqId, out var vec);
                            candidateTuples.Add((f.FaqId, vec, f.Question ?? string.Empty, f.SearchTextCache ?? string.Empty));
                        }

                            await WriteAppLogAsync("Debug", "Controller: ProviderCandidates FETCHED; starting scoring", new { ConversationId = req.ConversationId, Provider = embeddingProvider, CandidateCount = candidateTuples.Count }, null, db);
                            var provScoreStart = DateTime.UtcNow;
                            var scores = _scoring.ScoreCandidates(providerVec, candidateTuples, normalizedText, faqDict);
                            var provScoreMs = (DateTime.UtcNow - provScoreStart).TotalMilliseconds;
                            await WriteAppLogAsync("Debug", "Controller: Provider scoring END", new { ConversationId = req.ConversationId, Provider = embeddingProvider, CandidateCount = candidateTuples.Count, ElapsedMs = provScoreMs }, null, db);
                        var filtered = scores.Where(kv => kv.Value >= 0.0001).ToDictionary(kv => kv.Key, kv => kv.Value);
                        var ranked = filtered.OrderByDescending(kv => kv.Value).ToList();
                        if (ranked.Count == 0 && scores.Count > 0) ranked = scores.OrderByDescending(kv => kv.Value).ToList();

                        if (ranked.Count > 0)
                        {
                            var best = ranked[0];
                            // if provider gives a confident match, prefer provider
                            if (best.Value >= HIGH)
                            {
                                matchedFaqId = best.Key;
                                confidence = best.Value;
                                matchedBy = "embedding_provider";
                                replyText = faqDict.ContainsKey(matchedFaqId) ? faqDict[matchedFaqId].Answer : replyText;
                                route = "faq";
                                await WriteAppLogAsync("Information", "Provider rerank produced high confidence", new { ConversationId = req.ConversationId, FaqId = matchedFaqId, Score = confidence }, null, db);
                                // set queryVec to providerVec for downstream logging
                                queryVec = providerVec;
                            }
                            else
                            {
                                // provider did not find high confidence — fall back to local candidates
                                queryVec = localVec;
                                await WriteAppLogAsync("Information", "Provider rerank did not yield high confidence; falling back to local candidates", new { ConversationId = req.ConversationId, BestProviderScore = best.Value }, null, db);
                            }
                        }
                        else
                        {
                            queryVec = localVec;
                        }
                    }
                    else
                    {
                        // provider unavailable or no candidates — use local
                        queryVec = localVec;
                    }
                }
            }
            catch (Exception ex)
            {
                    await WriteAppLogAsync("Warning", "Embedding retrieval failed for text", new { Text = normalizedText }, ex, db);
                queryVec = null;
            }

            // If embedding not available, use keyword/token-overlap fallback to produce candidates
            if (queryVec == null)
            {
                // use SearchTextCache for fallback and limit scan size
                var faqsForFallback = await db.BotFaqItems.AsNoTracking()
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
                    topFaqIds = rankedFb.Select(kv => kv.Key).Take(GetIntConfig("bot.faq.topCandidates", 5)).ToList();
                    var bestFb = rankedFb[0];
                    matchedFaqId = bestFb.Key;
                    confidence = bestFb.Value;
                    await WriteAppLogAsync("Information", "Keyword fallback ranked: ConversationId={ConversationId} TopIds={TopIds}", new { ConversationId = req.ConversationId, TopIds = topFaqIds }, null, db);

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
                var isComposite = _textProcessing.IsComposite(normalizedText, tokens ?? Array.Empty<string>());

                // Build top candidate IDs via CandidateBuilderService
                List<string>? built = null;
                try
                {
                    built = await _candidateBuilder.BuildCandidatesAsync(normalizedText, queryVec, embeddingProvider, GetIntConfig("bot.faq.topCandidates", 5));
                }
                catch (Exception ex)
                {
                    await WriteAppLogAsync("Warning", "CandidateBuilder.BuildCandidatesAsync failed", new { Text = normalizedText }, ex, db);
                    built = null;
                }

                if (built != null && built.Count > 0)
                {
                    topFaqIds = built;
                    await WriteAppLogAsync("Information", "Candidates built: ConversationId={ConversationId} Ids={Ids}", new { ConversationId = req.ConversationId, Ids = topFaqIds }, null, db);

                    // fetch details for scoring/decision
                    List<ARCompletions.Domain.BotFaqItem> faqs;
                    try
                    {
                        faqs = await _faqService.FindByIdsAsync(topFaqIds, db);
                    }
                    catch (Exception ex)
                    {
                        await WriteAppLogAsync("Warning", "FaqService.FindByIdsAsync failed", new { Ids = topFaqIds }, ex, db);
                        faqs = new List<ARCompletions.Domain.BotFaqItem>();
                    }
                    await WriteAppLogAsync("Debug", "Fetched FAQ details: ConversationId={ConversationId} Count={Count}", new { ConversationId = req.ConversationId, Count = faqs.Count }, null, db);
                    var faqMap = faqs.ToDictionary(f => f.FaqId, f => f);

                    // compute scores for these candidates
                    var candidateTuples = new List<(string FaqId, double[]? Vec, string Question, string SearchTextCache)>();

                    // Batch fetch embeddings to avoid N+1 queries
                    var faqIds = faqs.Select(f => f.FaqId).ToList();
                    // Fetch latest embedding rows per FaqId for the selected provider
                    var embeddings = new List<ARCompletions.Domain.BotFaqEmbedding>();
                    const int batchSize = 100;
                    if (faqIds.Count <= batchSize)
                    {
                        embeddings = await FetchLatestEmbeddingsAsync(faqIds, embeddingProvider);
                    }
                    else
                    {
                        for (int i = 0; i < faqIds.Count; i += batchSize)
                        {
                            var chunk = faqIds.Skip(i).Take(batchSize).ToList();
                            var part = await FetchLatestEmbeddingsAsync(chunk, embeddingProvider);
                            embeddings.AddRange(part);
                        }
                    }

                    var embMap = embeddings.ToDictionary(e => e.FaqId, e => e.Embedding);
                    await WriteAppLogAsync("Debug", "Batch fetched embeddings: ConversationId={ConversationId} FetchedCount={Count}", new { ConversationId = req.ConversationId, FetchedCount = embeddings.Count }, null, db);

                    foreach (var f in faqs)
                    {
                        embMap.TryGetValue(f.FaqId, out var vec);
                        candidateTuples.Add((f.FaqId, vec, f.Question ?? string.Empty, f.SearchTextCache ?? string.Empty));
                    }

                    var scores = _scoring.ScoreCandidates(queryVec ?? Array.Empty<double>(), candidateTuples, normalizedText, faqMap);

                    // filter out extremely tiny/noise scores (guard)
                    var filtered = scores.Where(kv => kv.Value >= 0.0001).ToDictionary(kv => kv.Key, kv => kv.Value);
                    var ranked = filtered.OrderByDescending(kv => kv.Value).ToList();
                    // if all filtered out, fall back to original scores so analyzer can still see them
                    if (ranked.Count == 0 && scores.Count > 0)
                    {
                        ranked = scores.OrderByDescending(kv => kv.Value).ToList();
                    }
                    await WriteAppLogAsync("Debug", "Scoring results: ConversationId={ConversationId} Ranked={Ranked}", new { ConversationId = req.ConversationId, Ranked = string.Join(',', ranked.Select(kv => kv.Key + ":" + kv.Value)) }, null, db);

                    // Detailed per-candidate scoring logs when enabled
                    var detailedScoring = GetBool("DETAILED_SCORING_LOGS", false) || (Environment.GetEnvironmentVariable("DETAILED_SCORING_LOGS") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);
                    if (detailedScoring)
                    {
                        try
                        {
                            var details = _scoring.ScoreCandidatesDetailed(queryVec ?? Array.Empty<double>(), candidateTuples, normalizedText, faqMap);
                            var detailedLog = details.Select(d => new { d.FaqId, d.Cosine, d.QuestionSimilarity, d.KeywordScore, d.Overlap, d.FinalScore }).ToArray();
                            await WriteAppLogAsync("Information", "Scoring detailed: ConversationId={ConversationId} Candidates={Candidates}", new { ConversationId = req.ConversationId, Candidates = detailedLog }, null, db);

                            // Persist detailed scoring diagnostics into `app_logs` for easier debugging/analysis
                            try
                            {
                                var diagPayload = new
                                {
                                    ConversationId = req.ConversationId,
                                    QueryVecExists = queryVec != null,
                                    QueryVecLen = queryVec?.Length ?? 0,
                                    Candidates = details.Select(d => {
                                        var tuple = candidateTuples.FirstOrDefault(t => t.FaqId == d.FaqId);
                                        return new {
                                            d.FaqId,
                                            d.Cosine,
                                            d.QuestionSimilarity,
                                            d.KeywordScore,
                                            d.Overlap,
                                            d.FinalScore,
                                            EmbeddingPresent = tuple.Vec != null,
                                            EmbeddingLen = tuple.Vec?.Length ?? 0
                                        };
                                    }).ToArray()
                                };

                                await WriteAppLogAsync("Information", "Scoring detailed (debug): ConversationId={ConversationId}", diagPayload, null, db);
                            }
                            catch
                            {
                                // swallow to avoid affecting response
                            }
                        }
                        catch
                        {
                            // swallow to avoid affecting response
                        }
                    }

                    // Optionally force immediate DB write for scoring logs to increase log volume
                    var forceImmediateScoringLogs = (Environment.GetEnvironmentVariable("FORCE_IMMEDIATE_SCORING_LOGS") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);
                    if (forceImmediateScoringLogs && _dbLogger != null)
                    {
                        try
                        {
                            // write a copy that forces SaveChanges immediately using the request's db context
                            await _dbLogger.LogAsync(db, "Debug", "Scoring results (immediate): ConversationId={ConversationId} Ranked={Ranked}", new { ConversationId = req.ConversationId, Ranked = string.Join(',', ranked.Select(kv => kv.Key + ":" + kv.Value)) }, null, saveNow: true);

                            // also log top 3 entries individually to increase sample count
                            var topN = ranked.Take(3).ToList();
                            foreach (var kv in topN)
                            {
                                await _dbLogger.LogAsync(db, "Debug", "Scoring top: ConversationId={ConversationId} FaqId={FaqId} Score={Score}", new { ConversationId = req.ConversationId, FaqId = kv.Key, Score = kv.Value }, null, saveNow: true);
                            }
                        }
                        catch
                        {
                            // swallow to avoid affecting response
                        }
                    }
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

                        // Log the embedding decision details for diagnostics (after route chosen)
                        try
                        {
                            var calculatedMin = (faqMap.TryGetValue(matchedFaqId, out var bf) ? (bf.MinConfidenceScore ?? directLow) : directLow);
                            await WriteAppLogAsync("Information", "Embedding decision: ConversationId={ConversationId} BestScore={BestScore} MinScore={MinScore} AllowDirect={AllowDirect} IsComposite={IsComposite} Route={Route} MatchedBy={MatchedBy}", new { ConversationId = req.ConversationId, BestScore = confidence, MinScore = calculatedMin, AllowDirect = allowDirect, IsComposite = isComposite, Route = route, MatchedBy = matchedBy }, null, db);
                        }
                        catch { }
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
            await _stateService.SaveStateAsync(state, useMemoryState, stateCacheKey, deferSave: true, db);
        }

        if (route == "candidates" && topFaqIds.Count > 0)
        {
            // store as JsonDocument so DB column can be jsonb for querying/indexing
            var json = JsonSerializer.Serialize(topFaqIds);
            state.PendingDisambiguationIds = JsonDocument.Parse(json);
            state.PendingDisambiguationRoute = "faq";
            state.PendingDisambiguationAt = now;
            await WriteAppLogAsync("Debug", "Set pending disambiguation: ConversationId={ConversationId} Ids={Ids}", new { ConversationId = req.ConversationId, Ids = topFaqIds }, null, db);
        }
        else
        {
            state.PendingDisambiguationIds = null;
            state.PendingDisambiguationRoute = null;
            state.PendingDisambiguationAt = null;
        }
        state.UpdatedAt = now;
        await _stateService.SaveStateAsync(state, useMemoryState, stateCacheKey, deferSave: true, db);
        await WriteAppLogAsync("Debug", "Saved state: ConversationId={ConversationId} PendingIds={PendingIds}", new { ConversationId = req.ConversationId, PendingIds = state.PendingDisambiguationIds }, null, db);

        // 8) 組合回傳物件
        var shouldReply = route == "faq" || (route == "candidates" && topFaqIds.Count > 0);

        object[] quickReplies = Array.Empty<object>();
        if (route == "candidates" && topFaqIds.Count > 0)
        {
            var faqList = await db.BotFaqItems
                .AsNoTracking()
                .Where(f => topFaqIds.Contains(f.FaqId))
                .ToListAsync();
            var faqDict = faqList.ToDictionary(f => f.FaqId, f => f);

            var qr = new List<object>();
            var maxQuick = GetIntConfig("bot.quickReply.maxItems", 4);
            foreach (var id in topFaqIds.Take(maxQuick))
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
            await _routeLogger.LogRouteAsync(routeLog, persistRouteLogs3, db);

        // Instrumentation: record elapsed immediately after route logging
        try
        {
            await WriteAppLogAsync("Debug", "Checkpoint: after RouteLog", new { ElapsedMs = sw.ElapsedMilliseconds }, null, db);
        }
        catch { }

        if (deferredSaveNeeded)
        {
            try { await db.SaveChangesAsync(); }
            catch { /* swallow to avoid affecting response */ }
        }
        // Instrumentation: record elapsed after any deferred SaveChanges
        try
        {
            await WriteAppLogAsync("Debug", "Checkpoint: after SaveChanges", new { ElapsedMs = sw.ElapsedMilliseconds }, null, db);
        }
        catch { }
        sw.Stop();
        await WriteAppLogAsync("Information", "Query finished: ConversationId={ConversationId} Route={Route} ShouldReply={ShouldReply} ElapsedMs={Ms}", new { ConversationId = req.ConversationId, Route = route, ShouldReply = shouldReply, ElapsedMs = sw.ElapsedMilliseconds }, null, db);

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
    // Raw vendor event payload removed for privacy; do not persist raw event data.
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
