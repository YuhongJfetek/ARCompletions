using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using ARCompletions.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Controllers.Api;

[ApiController]
[Route("internal/v1")] // 由 Program.cs 的 middleware 保護 X-Internal-API-Key
public class BotController : ControllerBase
{
    private readonly ARCompletionsContext _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly IMemoryCache _cache;

    public BotController(ARCompletionsContext db, IEmbeddingService embeddingService, IMemoryCache cache)
    {
        _db = db;
        _embeddingService = embeddingService;
        _cache = cache;
    }

    private static double CosineSimilarity(double[] a, double[] b)
    {
        if (a == null || b == null) return 0.0;
        var len = Math.Min(a.Length, b.Length);
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < len; i++)
        {
            var va = a[i];
            var vb = b[i];
            dot += va * vb;
            na += va * va;
            nb += vb * vb;
        }
        if (na == 0 || nb == 0) return 0.0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    private static double TokenOverlapScore(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0.0;
        var ta = Regex.Matches(a.ToLowerInvariant(), "\\w+").Select(m => m.Value).Distinct();
        var tb = Regex.Matches(b.ToLowerInvariant(), "\\w+").Select(m => m.Value).Distinct();
        var setA = new HashSet<string>(ta);
        var setB = new HashSet<string>(tb);
        if (setA.Count == 0 || setB.Count == 0) return 0.0;
        var inter = setA.Intersect(setB).Count();
        var uni = setA.Union(setB).Count();
        return uni == 0 ? 0.0 : (double)inter / uni;
    }

    private static string[] ParseStringArrayJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return doc.RootElement
                    .EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s!)
                    .ToArray();
            }
        }
        catch
        {
            // ignore
        }
        return Array.Empty<string>();
    }

    // A1 查詢決策 API（簡化版：目前僅回傳 shouldReply=false 骨架，後續可接上實際判斷流程）
    [HttpPost("bot/query")]
    public async Task<IActionResult> Query([FromBody] BotQueryRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.ConversationId))
        {
            return BadRequest(new { error = "invalid request" });
        }

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
        if (persistIncoming)
        {
            _db.BotIncomingEvents.Add(ev);
            await _db.SaveChangesAsync();
        }

        // 2) 讀取會話狀態（handoff 中則不再回覆）
        var useMemoryState = (Environment.GetEnvironmentVariable("USE_INMEMORY_STATE") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);
        BotConversationState? state = null;
        var stateCacheKey = $"state:{sourceType}:{req.ConversationId}";
        if (useMemoryState)
        {
            _cache.TryGetValue(stateCacheKey, out state);
        }
        else
        {
            state = await _db.BotConversationStates.FindAsync(sourceType, req.ConversationId);
        }
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
                await _db.SaveChangesAsync();
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
                await _db.SaveChangesAsync();
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

        // 4) 先做 FAQ 問題精準比對（完全相同的問題直接回覆）
        if (route == "none")
        {
            var directFaq = await _db.BotFaqItems
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Enabled && f.Question == req.Text);

            if (directFaq != null)
            {
                route = "faq";
                matchedFaqId = directFaq.FaqId;
                matchedBy = "faq_exact";
                confidence = 1.0;
                replyText = directFaq.Answer;
                faqCategory = directFaq.CategoryKey ?? directFaq.Category;
                needsHumanHandoff = directFaq.NeedsHumanHandoff;
            }
        }

        // 5) 若尚未決策，做 alias 精準比對
        if (route == "none")
        {
            var alias = await _db.BotFaqAliases
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Enabled && a.Term == req.Text);

            if (alias != null)
            {
                aliasTerm = alias.Term;
                var aliasFaqIds = ParseStringArrayJson(alias.FaqIds);

                if (string.Equals(alias.Mode, "direct", StringComparison.OrdinalIgnoreCase) && aliasFaqIds.Length == 1)
                {
                    matchedFaqId = aliasFaqIds[0];
                    matchedBy = "alias_direct";
                    confidence = 1.0;
                    route = "faq";
                    faqCategory = null;

                    var faq = await _db.BotFaqItems.AsNoTracking().FirstOrDefaultAsync(f => f.FaqId == matchedFaqId && f.Enabled);
                    if (faq != null)
                    {
                        replyText = faq.Answer;
                        faqCategory = faq.CategoryKey ?? faq.Category;
                        needsHumanHandoff = faq.NeedsHumanHandoff;
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
            const double defaultDirectHigh = 0.60; // 高信心暫未區分使用
            const double defaultCosineWeight = 0.7;
            const double defaultOverlapWeight = 0.3;

            var settings = await _db.BotConstantsConfigs
                .AsNoTracking()
                .ToListAsync();

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
                // caching for embeddings per text+model
                var cacheKey = $"embedding:{modelName}:{req.Text}";
                var cacheTtlSeconds = int.TryParse(Environment.GetEnvironmentVariable("EMBEDDING_CACHE_TTL_SECONDS"), out var s) ? s : 300;
                string? embJson = null;
                if (_cache.TryGetValue<string>(cacheKey, out var cachedJson))
                {
                    embJson = cachedJson;
                }
                else
                {
                    embJson = await _embeddingService.GetEmbeddingJsonAsync(req.Text, modelName);
                    // local fallback: if service didn't return an embedding, try to find precomputed local vector
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
                                            if (string.Equals(txt.Trim(), req.Text.Trim(), StringComparison.OrdinalIgnoreCase))
                                            {
                                                var list = new List<double>();
                                                foreach (var v in embEl.EnumerateArray()) list.Add(v.GetDouble());
                                                var wrapper = new { data = new[] { new { embedding = list } } };
                                                embJson = JsonSerializer.Serialize(wrapper);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                embJson = null;
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(embJson))
                    {
                        _cache.Set(cacheKey, embJson, TimeSpan.FromSeconds(cacheTtlSeconds));
                    }
                }

                if (!string.IsNullOrWhiteSpace(embJson))
                {
                    using var doc = JsonDocument.Parse(embJson);
                    if (doc.RootElement.TryGetProperty("data", out var dataElem) && dataElem.GetArrayLength() > 0)
                    {
                        var embElem = dataElem[0].GetProperty("embedding");
                        var list = new List<double>();
                        foreach (var v in embElem.EnumerateArray())
                        {
                            list.Add(v.GetDouble());
                        }
                        if (list.Count > 0)
                        {
                            queryVec = list.ToArray();
                        }
                    }
                }
            }
            catch
            {
                queryVec = null;
            }

            if (queryVec != null && queryVec.Length > 0)
            {
                var embItems = await _db.BotFaqEmbeddings
                    .AsNoTracking()
                    .Where(e => e.IsActive && e.EmbeddingProvider == embeddingProvider)
                    .ToListAsync();

                if (embItems.Count > 0)
                {
                    var faqIds = embItems.Select(e => e.FaqId).Distinct().ToList();
                    var faqs = await _db.BotFaqItems
                        .AsNoTracking()
                        .Where(f => faqIds.Contains(f.FaqId) && f.Enabled)
                        .ToListAsync();
                    var faqMap = faqs.ToDictionary(f => f.FaqId, f => f);

                    var scores = new Dictionary<string, double>();

                    foreach (var it in embItems)
                    {
                        if (!faqMap.ContainsKey(it.FaqId)) continue;
                        var v = it.Embedding ?? Array.Empty<double>();
                        if (v.Length == 0) continue;

                        var cos = CosineSimilarity(queryVec, v);
                        var textForOverlap = it.SearchText ?? it.Question ?? faqMap[it.FaqId].SearchTextCache ?? faqMap[it.FaqId].Question;
                        var overlap = TokenOverlapScore(req.Text, textForOverlap ?? string.Empty);
                        var composite = cos * cosineWeight + overlap * overlapWeight;

                        if (!scores.ContainsKey(it.FaqId) || composite > scores[it.FaqId])
                        {
                            scores[it.FaqId] = composite;
                        }
                    }

                    if (scores.Count > 0)
                    {
                        var ranked = scores.OrderByDescending(kv => kv.Value).ToList();
                        topFaqIds = ranked.Select(kv => kv.Key).Take(5).ToList();
                        var best = ranked[0];
                        matchedFaqId = best.Key;
                        confidence = best.Value;

                        var bestFaq = faqMap[matchedFaqId];
                        var minScore = bestFaq.MinConfidenceScore ?? directLow;

                        if (allowDirect && confidence >= minScore)
                        {
                            route = "faq";
                            matchedBy = "embedding";
                            var faq = bestFaq;
                            replyText = faq.Answer;
                            faqCategory = faq.CategoryKey ?? faq.Category;
                            needsHumanHandoff = faq.NeedsHumanHandoff;
                        }
                        else
                        {
                            route = "candidates";
                            matchedBy = "embedding_low_conf";
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
            if (useMemoryState)
            {
                _cache.Set(stateCacheKey, state, TimeSpan.FromHours(1));
            }
            else
            {
                _db.BotConversationStates.Add(state);
                await _db.SaveChangesAsync();
            }
        }

        if (route == "candidates" && topFaqIds.Count > 0)
        {
            state.PendingDisambiguationIds = JsonSerializer.Serialize(topFaqIds);
            state.PendingDisambiguationRoute = "faq";
            state.PendingDisambiguationAt = now;
        }
        else
        {
            state.PendingDisambiguationIds = null;
            state.PendingDisambiguationRoute = null;
            state.PendingDisambiguationAt = null;
        }
        state.UpdatedAt = now;
        if (useMemoryState)
        {
            _cache.Set(stateCacheKey, state, TimeSpan.FromHours(1));
        }
        else
        {
            await _db.SaveChangesAsync();
        }

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

        var resp = new BotQueryResponse
        {
            ShouldReply = shouldReply,
            Route = route,
            MatchedFaqId = matchedFaqId,
            MatchedBy = matchedBy,
            Confidence = confidence,
            ReplyText = replyText,
            ReplyMode = route == "faq" ? "faq" : route,
            QuickReplyItems = quickReplies,
            StateChanges = new BotQueryStateChanges
            {
                BotEnabled = botEnabled,
                HandoffUntil = handoffUntil,
                PendingDisambiguationIds = route == "candidates" && topFaqIds.Count > 0 ? topFaqIds.ToArray() : Array.Empty<string>(),
                PendingDisambiguationRoute = route == "candidates" && topFaqIds.Count > 0 ? "faq" : null
            },
            LogPayload = new BotQueryLogPayload
            {
                FaqCategory = faqCategory,
                LlmEnabled = false,
                NeedsHumanHandoff = needsHumanHandoff,
                IsStaffTriggered = false,
                ContextCountBefore = contextBefore,
                ContextCountAfter = contextAfter
            }
        };

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
        if (persistRouteLogs3)
        {
            _db.BotMessageRoutes.Add(routeLog);
            await _db.SaveChangesAsync();
        }

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
