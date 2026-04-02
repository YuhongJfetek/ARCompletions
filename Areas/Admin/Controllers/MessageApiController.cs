using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class MessageApiController : Controller
{
    private readonly ARCompletionsContext _db;
    private static readonly object _logLock = new();
    private static readonly List<WebhookLog> _logs = new();

    private const string Prefix = "MessageApi:";

    public MessageApiController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var all = await _db.SystemSettings
            .AsNoTracking()
            .Where(s => s.SettingKey.StartsWith(Prefix))
            .ToListAsync();

        var dict = all.ToDictionary(
            s => s.SettingKey.Substring(Prefix.Length),
            s => s,
            StringComparer.OrdinalIgnoreCase);

        ViewBag.Settings = dict;

        var backendKey = Environment.GetEnvironmentVariable("BACKEND_API_KEY")
                          ?? string.Empty;
        ViewBag.BackendApiKeyConfigured = !string.IsNullOrWhiteSpace(backendKey);
        ViewBag.BackendApiKeyPreview = !string.IsNullOrWhiteSpace(backendKey) && backendKey.Length >= 4
            ? backendKey.Substring(0, 4) + new string('*', Math.Max(0, backendKey.Length - 4))
            : null;

        lock (_logLock)
        {
            ViewBag.WebhookLogs = _logs
                .OrderByDescending(l => l.Timestamp)
                .ToList();
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string? webhookUrl, string? apiKey, string? webhookSecret, bool enableWebhook = false)
    {
        await UpsertSettingAsync("WebhookUrl", webhookUrl ?? string.Empty);
        await UpsertSettingAsync("ApiKey", apiKey ?? string.Empty);
        await UpsertSettingAsync("WebhookSecret", webhookSecret ?? string.Empty);
        await UpsertSettingAsync("EnableWebhook", enableWebhook ? "true" : "false");

        TempData["Message"] = "已儲存訊息 API 設定";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ClearLogs()
    {
        lock (_logLock)
        {
            _logs.Clear();
        }
        TempData["Message"] = "已清除 Webhook 測試紀錄";
        return RedirectToAction(nameof(Index));
    }

    public sealed class TestWebhookRequest
    {
        public string? Url { get; set; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestWebhookAjax([FromBody] TestWebhookRequest req)
    {
        string? url = req?.Url;

        if (string.IsNullOrWhiteSpace(url))
        {
            var all = await _db.SystemSettings
                .AsNoTracking()
                .Where(s => s.SettingKey == Prefix + "WebhookUrl")
                .ToListAsync();
            var setting = all.FirstOrDefault();
            url = setting?.SettingValue;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            return Json(new { success = false, message = "Webhook URL 未設定" });
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var initiatedBy = User?.Identity?.Name ?? "admin";

        try
        {
            using var client = new HttpClient();
            var payload = new { type = "test", source = "Admin", ts = now };
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync(url, content);
            var body = await resp.Content.ReadAsStringAsync();

            var log = new WebhookLog
            {
                Timestamp = now,
                Url = url,
                Success = resp.IsSuccessStatusCode,
                InitiatedBy = initiatedBy,
                Error = resp.IsSuccessStatusCode ? null : body,
                Response = resp.IsSuccessStatusCode ? body : null
            };

            lock (_logLock)
            {
                _logs.Add(log);
                if (_logs.Count > 100)
                {
                    _logs.RemoveRange(0, _logs.Count - 100);
                }
            }

            return Json(new
            {
                success = resp.IsSuccessStatusCode,
                status = (int)resp.StatusCode,
                reason = resp.ReasonPhrase,
                response = body
            });
        }
        catch (Exception ex)
        {
            var log = new WebhookLog
            {
                Timestamp = now,
                Url = url,
                Success = false,
                InitiatedBy = initiatedBy,
                Error = ex.Message,
                Response = null
            };

            lock (_logLock)
            {
                _logs.Add(log);
                if (_logs.Count > 100)
                {
                    _logs.RemoveRange(0, _logs.Count - 100);
                }
            }

            return Json(new { success = false, message = ex.Message });
        }
    }

    private async Task UpsertSettingAsync(string shortKey, string value)
    {
        var fullKey = Prefix + shortKey;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var user = User?.Identity?.Name ?? "system";

        var existing = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == fullKey);
        if (existing == null)
        {
            existing = new SystemSetting
            {
                Id = Guid.NewGuid().ToString("N"),
                SettingKey = fullKey,
                SettingValue = value,
                UpdatedAt = now,
                UpdatedBy = user
            };
            _db.SystemSettings.Add(existing);
        }
        else
        {
            existing.SettingValue = value;
            existing.UpdatedAt = now;
            existing.UpdatedBy = user;
        }

        await _db.SaveChangesAsync();
    }

    private sealed class WebhookLog
    {
        public long Timestamp { get; set; }
        public string Url { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? InitiatedBy { get; set; }
        public string? Error { get; set; }
        public string? Response { get; set; }
    }
}
