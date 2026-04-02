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
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Controllers.Api;

[ApiController]
[Route("internal/v1")] // 由 Program.cs 的 middleware 保護 X-Internal-API-Key
public class BotController : ControllerBase
{
    private readonly ARCompletionsContext _db;
    private readonly IEmbeddingService _embeddingService;

    public BotController(ARCompletionsContext db, IEmbeddingService embeddingService)
    {
        _db = db;
        _embeddingService = embeddingService;
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

        // Npgsql 8 僅接受 Offset=0 (UTC) 的 DateTimeOffset，統一轉成 UTC 後再寫入 DB
        var receivedAt = req.ReceivedAt == default
            ? now
            : req.ReceivedAt.ToUniversalTime();

        // 1) 寫入 incoming event
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
        _db.BotIncomingEvents.Add(ev);
        await _db.SaveChangesAsync();

        // 2) 讀取會話狀態（handoff 中則不再回覆）
        var state = await _db.BotConversationStates.FindAsync(sourceType, req.ConversationId);
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
            _db.BotMessageRoutes.Add(logHandoff);
            await _db.SaveChangesAsync();

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
            _db.BotMessageRoutes.Add(logEmpty);
            await _db.SaveChangesAsync();

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

        // 4) 先做 alias 精準比對
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

        // 5) 若尚未決策，改用 Embedding 搜尋
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
                var embJson = await _embeddingService.GetEmbeddingJsonAsync(req.Text, modelName);
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

                        if (allowDirect && confidence >= directLow)
                        {
                            route = "faq";
                            matchedBy = "embedding";
                            var faq = faqMap[matchedFaqId];
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

        // 6) 更新會話狀態（針對 disambiguation）
        if (state == null)
        {
            state = new BotConversationState
            {
                SourceType = sourceType,
                ConversationId = req.ConversationId,
                UpdatedAt = now
            };
            _db.BotConversationStates.Add(state);
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
        await _db.SaveChangesAsync();

        // 7) 組合回傳物件
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

        // 8) 寫入 route log
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
        _db.BotMessageRoutes.Add(routeLog);
        await _db.SaveChangesAsync();

        return Ok(resp);
    }

    // A2: 事件寫入
    [HttpPost("events")]
    public async Task<IActionResult> CreateEvent([FromBody] BotEventCreateRequest req)
    {
        if (req == null) return BadRequest(new { error = "invalid request" });

        var now = DateTimeOffset.UtcNow;
        var receivedAt = req.ReceivedAt == default
            ? now
            : req.ReceivedAt.ToUniversalTime();

        var ev = new BotIncomingEvent
        {
            RawEventJson = req.RawEventJson ?? "{}",
            EventType = req.EventType,
            MessageType = req.MessageType,
            SourceType = req.SourceType,
            LineUserId = req.LineUserId,
            LineGroupId = req.LineGroupId,
            LineRoomId = req.LineRoomId,
            ConversationId = req.ConversationId,
            ReplyToken = req.ReplyToken,
            ReceivedAt = receivedAt
        };

        _db.BotIncomingEvents.Add(ev);
        await _db.SaveChangesAsync();

        return Created(string.Empty, new { eventRowId = ev.EventRowId });
    }

    // A2: 路由寫入
    [HttpPost("routes")]
    public async Task<IActionResult> CreateRoute([FromBody] BotRouteCreateRequest req)
    {
        if (req == null) return BadRequest(new { error = "invalid request" });

        var r = new BotMessageRoute
        {
            EventRowId = req.EventRowId,
            ConversationId = req.ConversationId,
            SourceType = req.SourceType,
            LineUserId = req.LineUserId,
            LogEvent = req.LogEvent,
            Route = req.Route,
            Reason = req.Reason,
            MatchedFaqId = req.MatchedFaqId,
            MatchedScore = req.MatchedScore,
            MatchedBy = req.MatchedBy,
            FaqCategory = req.FaqCategory,
            TopFaqIds = req.TopFaqIds,
            AliasTerm = req.AliasTerm,
            ReplyText = req.ReplyText,
            LlmEnabled = req.LlmEnabled,
            NeedsHumanHandoff = req.NeedsHumanHandoff,
            IsStaffTriggered = req.IsStaffTriggered,
            ContextCountBefore = req.ContextCountBefore,
            ContextCountAfter = req.ContextCountAfter,
            LogClass = req.LogClass,
            LogGroup = req.LogGroup,
            LogPriority = req.LogPriority,
            LogUseful = req.LogUseful,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.BotMessageRoutes.Add(r);
        await _db.SaveChangesAsync();

        return Created(string.Empty, new { routeRowId = r.RouteRowId });
    }

    // A2: LLM log 寫入
    [HttpPost("llm-logs")]
    public async Task<IActionResult> CreateLlmLog([FromBody] BotLlmLogCreateRequest req)
    {
        if (req == null) return BadRequest(new { error = "invalid request" });

        var log = new BotLlmLog
        {
            EventRowId = req.EventRowId,
            LogEvent = req.LogEvent,
            Task = req.Task,
            FaqId = req.FaqId,
            MatchedBy = req.MatchedBy,
            Confidence = req.Confidence,
            Model = req.Model,
            ReplyMode = req.ReplyMode,
            Reason = req.Reason,
            ErrorMessage = req.ErrorMessage,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.BotLlmLogs.Add(log);
        await _db.SaveChangesAsync();

        return Created(string.Empty, new { llmLogId = log.LlmLogId });
    }
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

public class BotEventCreateRequest
{
    public string? RawEventJson { get; set; }
    public string? EventType { get; set; }
    public string? MessageType { get; set; }
    public string? SourceType { get; set; }
    public string? LineUserId { get; set; }
    public string? LineGroupId { get; set; }
    public string? LineRoomId { get; set; }
    public string? ConversationId { get; set; }
    public string? ReplyToken { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}

public class BotRouteCreateRequest
{
    public long? EventRowId { get; set; }
    public string? ConversationId { get; set; }
    public string? SourceType { get; set; }
    public string? LineUserId { get; set; }
    public string? LogEvent { get; set; }
    public string? Route { get; set; }
    public string? Reason { get; set; }
    public string? MatchedFaqId { get; set; }
    public double? MatchedScore { get; set; }
    public string? MatchedBy { get; set; }
    public string? FaqCategory { get; set; }
    public string? TopFaqIds { get; set; }
    public string? AliasTerm { get; set; }
    public string? ReplyText { get; set; }
    public bool? LlmEnabled { get; set; }
    public bool? NeedsHumanHandoff { get; set; }
    public bool? IsStaffTriggered { get; set; }
    public int? ContextCountBefore { get; set; }
    public int? ContextCountAfter { get; set; }
    public string? LogClass { get; set; }
    public string? LogGroup { get; set; }
    public string? LogPriority { get; set; }
    public bool? LogUseful { get; set; }
}

public class BotLlmLogCreateRequest
{
    public long? EventRowId { get; set; }
    public string? LogEvent { get; set; }
    public string? Task { get; set; }
    public string? FaqId { get; set; }
    public string? MatchedBy { get; set; }
    public double? Confidence { get; set; }
    public string? Model { get; set; }
    public string? ReplyMode { get; set; }
    public string? Reason { get; set; }
    public string? ErrorMessage { get; set; }
}
