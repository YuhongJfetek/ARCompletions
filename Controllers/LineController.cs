using System;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ARCompletions.Data;
using ARCompletions.Services;

namespace ARCompletions.Controllers
{
    /// <summary>
    /// 處理 LINE Bot Webhook 事件與簡單健康檢查的控制器。
    /// </summary>
    /// <remarks>
    /// 路由：
    /// - POST /api/line/webhook ：LINE Webhook 端點（需帶入 X-Line-Signature 標頭）
    /// - POST /api/webhook/line ：舊版別名，保留與先前端點的相容性
    /// - GET  /api/line/ping    ：健康檢查（回傳 "ok"）
    /// </remarks>
    [ApiController]
    [Route("api/line")]
    public class LineController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILineService _lineSvc;
        private readonly ARCompletions.Data.ARCompletionsContext _db;

        public LineController(IConfiguration config, ILineService lineSvc, ARCompletions.Data.ARCompletionsContext db)
        {
            _config = config;
            _lineSvc = lineSvc;
            _db = db;
        }

        /// <summary>
        /// LINE 控制器的簡易健康檢查端點。
        /// </summary>
        /// <returns>服務可連線時回傳 HTTP 200，內容為 "ok"。</returns>
        [HttpGet("ping")]
        public IActionResult Ping() => Ok("ok");

        /// <summary>
        /// LINE 事件的主要 Webhook 入口。驗證 `X-Line-Signature` 標頭，並處理請求中的每個事件。
        /// </summary>
        /// <remarks>
        /// 輸入：
        /// - Body：LINE 傳來的 JSON 物件，包含 `events` 陣列（簽章驗證使用原始請求本文）。
        /// - Header：`X-Line-Signature` 應為 LINE 使用 channel secret 計算出的 HMAC-SHA256（Base64）。
        ///
        /// 行為：
        /// - 將每個事件寫入 `LineEventLog`，並對應或建立 `LineUser`（upsert）。
        /// - 對於簡單事件（follow、文字訊息、postback）會同步使用 <see cref="ARCompletions.Services.LineBotService"/> 回覆。
        /// - 若驗證失敗回傳 401；處理成功回傳 200；內部錯誤回傳 500。
        /// </remarks>
        /// <returns>
        /// - 200 OK：事件已接收並處理或無事件可處理。
        /// - 401 Unauthorized：簽章驗證失敗。
        /// - 500 Internal Server Error：處理發生例外。
        /// </returns>
        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            var secret = _config["LINE_CHANNEL_SECRET"];
            if (string.IsNullOrWhiteSpace(secret)) return StatusCode(500, "Missing LINE_CHANNEL_SECRET");

            Request.EnableBuffering();
            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync();
                Request.Body.Position = 0;
            }

            var signature = Request.Headers["X-Line-Signature"].ToString();
            if (!ValidateSignature(secret, body, signature))
                return Unauthorized("Invalid signature");

            try
            {
                var json = System.Text.Json.JsonDocument.Parse(body);
                if (!json.RootElement.TryGetProperty("events", out var events) || events.GetArrayLength() == 0)
                    return Ok();

                foreach (var ev in events.EnumerateArray())
                {
                    var evType = ev.GetProperty("type").GetString() ?? "";
                    string replyToken = ev.TryGetProperty("replyToken", out var rt) && rt.ValueKind == System.Text.Json.JsonValueKind.String ? rt.GetString()! : string.Empty;
                    string userId = ev.TryGetProperty("source", out var src) && src.ValueKind == System.Text.Json.JsonValueKind.Object && src.TryGetProperty("userId", out var uid) && uid.ValueKind == System.Text.Json.JsonValueKind.String ? uid.GetString()! : "(unknown)";
                    string messageType = ev.TryGetProperty("message", out var msg) && msg.ValueKind == System.Text.Json.JsonValueKind.Object && msg.TryGetProperty("type", out var mt) ? mt.GetString() ?? string.Empty : string.Empty;
                    string text = msg.ValueKind == System.Text.Json.JsonValueKind.Object && msg.TryGetProperty("text", out var txt) ? txt.GetString() ?? string.Empty : string.Empty;
                    string postbackData = ev.TryGetProperty("postback", out var pb) && pb.ValueKind == System.Text.Json.JsonValueKind.Object && pb.TryGetProperty("data", out var pd) ? pd.GetString() ?? string.Empty : string.Empty;

                    // delegate persistence, enqueue and reply decision to LineService
                    await _lineSvc.HandleEventAsync(evType, replyToken, userId, messageType, text, postbackData, ev.GetRawText());
                }

                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "failed to process events", detail = ex.Message });
            }
        }

        private static bool ValidateSignature(string secret, string body, string signature)
        {
            try
            {
                var key = Encoding.UTF8.GetBytes(secret);
                using var hmac = new HMACSHA256(key);
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
                var computed = Convert.ToBase64String(hash);
                // constant-time comparison
                var a = Convert.FromBase64String(computed);
                var b = Convert.FromBase64String(signature);
                return CryptographicOperations.FixedTimeEquals(a, b);
            }
            catch
            {
                return false;
            }
        }

        private static System.Collections.Generic.Dictionary<string, string> ParseQuery(string data)
        {
            var dict = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
