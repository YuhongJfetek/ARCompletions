using System;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Services
{
    public class LineService : ILineService
    {
        private readonly ARCompletionsContext _db;
        private readonly LineBotService _bot;

        public LineService(ARCompletionsContext db, LineBotService bot)
        {
            _db = db;
            _bot = bot;
        }

        /// <summary>
        /// Persist the incoming LINE event, upsert the user and create an AnalysisJob in a single transaction.
        /// </summary>
        public async Task HandleEventAsync(string evType, string replyToken, string userId, string messageType, string text, string postbackData, string rawJson)
        {
            await using (var tx = await _db.Database.BeginTransactionAsync())
            {
                var log = new LineEventLog
                {
                    LineUserId = userId,
                    EventType = evType,
                    MessageType = string.IsNullOrEmpty(messageType) ? null : messageType,
                    Text = string.IsNullOrEmpty(text) ? (string.IsNullOrEmpty(postbackData) ? null : postbackData) : text,
                    RawJson = rawJson,
                    CreatedAt = DateTime.UtcNow
                };
                await _db.LineEventLogs.AddAsync(log);

                var u = await _db.LineUsers.FirstOrDefaultAsync(x => x.LineUserId == userId);
                if (u == null)
                {
                    u = new LineUser { LineUserId = userId, IsFollowed = evType == "follow" };
                    await _db.LineUsers.AddAsync(u);
                }
                u.LastSeenAt = DateTime.UtcNow;

                var jobParams = System.Text.Json.JsonSerializer.Serialize(new { eventLogId = "", userId = userId, type = evType });
                var job = new AnalysisJob
                {
                    Type = "EventAnalysis",
                    Params = jobParams,
                    Status = "Queued"
                };
                _db.AnalysisJobs.Add(job);

                await _db.SaveChangesAsync();

                jobParams = System.Text.Json.JsonSerializer.Serialize(new { eventLogId = log.Id, userId = userId, type = evType });
                job.Params = jobParams;
                _db.AnalysisJobs.Update(job);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
            }

            // make reply decisions and send replies (non-blocking to DB transaction)
            try
            {
                if (!string.IsNullOrWhiteSpace(replyToken))
                {
                    if (evType == "follow")
                    {
                        await _bot.ReplyText(replyToken, "嗨～歡迎加入！輸入「menu」看功能。");
                        return;
                    }

                    if (evType == "message" && messageType == "text")
                    {
                        var t = (text ?? string.Empty).Trim();
                        if (t.Equals("menu", StringComparison.OrdinalIgnoreCase) || t == "選單")
                        {
                            await _bot.ReplyFlex(replyToken, LineFlexTemplates.MainMenu());
                        }
                        else
                        {
                            await _bot.ReplyText(replyToken, $"你說：{t}\n輸入 menu 看選單");
                        }
                        return;
                    }

                    if (evType == "postback")
                    {
                        var action = ParseQuery(postbackData).GetValueOrDefault("action");
                        if (action == "help")
                            await _bot.ReplyText(replyToken, "請描述你的問題，我會幫你轉交。");
                        else
                            await _bot.ReplyText(replyToken, $"收到 postback：{postbackData}");
                    }
                }
            }
            catch
            {
                // swallow reply errors to avoid failing webhook processing
            }

        }
        
        private static System.Collections.Generic.Dictionary<string, string> ParseQuery(string data)
        {
            var dict = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(data)) return dict;
            foreach (var part in data.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                var k = Uri.UnescapeDataString(kv[0]);
                var v = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
                dict[k] = v;
            }
            return dict;
        }
    }
}
