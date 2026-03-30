using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
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
    private const string LogKey = "MessageApi:WebhookTestLogs";
    private const int MaxLogs = 100;

    public MessageApiController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var settings = await _db.SystemSettings
            .Where(s => s.SettingKey.StartsWith("MessageApi:"))
            .ToListAsync();

        var dict = settings.ToDictionary(s => s.SettingKey.Substring("MessageApi:".Length), s => s);
        ViewBag.Settings = dict;
        // load logs
        var logs = await ReadWebhookTestLogsAsync();
        ViewBag.WebhookLogs = logs;
        ViewBag.TestResult = TempData["WebhookTestResult"];
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string webhookUrl, string apiKey, string webhookSecret, bool enableWebhook = false)
    {
        async Task Upsert(string key, string value, string? desc = null)
        {
            var sk = "MessageApi:" + key;
            var existing = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == sk);
            if (existing == null)
            {
                _db.SystemSettings.Add(new SystemSetting
                {
                    Id = Guid.NewGuid().ToString("N"),
                    SettingKey = sk,
                    SettingValue = value ?? string.Empty,
                    Description = desc,
                    UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    UpdatedBy = User?.Identity?.Name
                });
            }
            else
            {
                existing.SettingValue = value ?? string.Empty;
                existing.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                existing.UpdatedBy = User?.Identity?.Name;
                _db.SystemSettings.Update(existing);
            }
        }

        await Upsert("WebhookUrl", webhookUrl);
        await Upsert("ApiKey", apiKey);
        await Upsert("WebhookSecret", webhookSecret);
        await Upsert("EnableWebhook", enableWebhook ? "true" : "false");

        await _db.SaveChangesAsync();
        TempData["Message"] = "Settings saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestWebhook(string url)
    {
        // pick url from parameter or from settings
        string testUrl = url;
        if (string.IsNullOrWhiteSpace(testUrl))
        {
            var ws = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "MessageApi:WebhookUrl");
            testUrl = ws?.SettingValue;
        }

        if (string.IsNullOrWhiteSpace(testUrl))
        {
            TempData["WebhookTestResult"] = "No webhook URL configured.";
            return RedirectToAction(nameof(Index));
        }

        long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var payload = new { type = "webhook_test", timestamp = ts };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await client.PostAsync(testUrl, content);
            var body = await resp.Content.ReadAsStringAsync();
            TempData["WebhookTestResult"] = $"Status: {(int)resp.StatusCode} {resp.ReasonPhrase}, Response: {body}";
            await AppendWebhookTestLogAsync(new WebhookTestLogEntry {
                Id = Guid.NewGuid().ToString("N"), Timestamp = ts, Url = testUrl, Success = true,
                StatusCode = (int)resp.StatusCode, Reason = resp.ReasonPhrase, Response = body, InitiatedBy = User?.Identity?.Name
            });
        }
        catch (Exception ex)
        {
            TempData["WebhookTestResult"] = "Error: " + ex.Message;
            await AppendWebhookTestLogAsync(new WebhookTestLogEntry {
                Id = Guid.NewGuid().ToString("N"), Timestamp = ts, Url = testUrl, Success = false,
                Error = ex.Message, InitiatedBy = User?.Identity?.Name
            });
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<JsonResult> TestWebhookAjax([FromBody] TestRequest req)
    {
        string testUrl = req?.Url;
        if (string.IsNullOrWhiteSpace(testUrl))
        {
            var ws = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "MessageApi:WebhookUrl");
            testUrl = ws?.SettingValue;
        }

        if (string.IsNullOrWhiteSpace(testUrl))
            return Json(new { success = false, message = "No webhook URL configured." });

        long ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            var payload = new { type = "webhook_test", timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await client.PostAsync(testUrl, content);
            var body = await resp.Content.ReadAsStringAsync();
            await AppendWebhookTestLogAsync(new WebhookTestLogEntry {
                Id = Guid.NewGuid().ToString("N"), Timestamp = ts, Url = testUrl, Success = true,
                StatusCode = (int)resp.StatusCode, Reason = resp.ReasonPhrase, Response = body, InitiatedBy = User?.Identity?.Name
            });
            return Json(new { success = true, status = (int)resp.StatusCode, reason = resp.ReasonPhrase, response = body });
        }
        catch (Exception ex)
        {
            await AppendWebhookTestLogAsync(new WebhookTestLogEntry {
                Id = Guid.NewGuid().ToString("N"), Timestamp = ts, Url = testUrl, Success = false,
                Error = ex.Message, InitiatedBy = User?.Identity?.Name
            });
            return Json(new { success = false, message = ex.Message });
        }

    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearLogs()
    {
        var existing = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == LogKey);
        if (existing != null)
        {
            _db.SystemSettings.Remove(existing);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    public class TestRequest
    {
        public string? Url { get; set; }
    }

    public class WebhookTestLogEntry
    {
        public string Id { get; set; } = string.Empty;
        public long Timestamp { get; set; }
        public string? Url { get; set; }
        public bool Success { get; set; }
        public int? StatusCode { get; set; }
        public string? Reason { get; set; }
        public string? Response { get; set; }
        public string? Error { get; set; }
        public string? InitiatedBy { get; set; }
    }

    private async Task<List<WebhookTestLogEntry>> ReadWebhookTestLogsAsync()
    {
        var existing = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == LogKey);
        if (existing == null || string.IsNullOrWhiteSpace(existing.SettingValue)) return new List<WebhookTestLogEntry>();
        try
        {
            var list = JsonSerializer.Deserialize<List<WebhookTestLogEntry>>(existing.SettingValue);
            return list ?? new List<WebhookTestLogEntry>();
        }
        catch
        {
            return new List<WebhookTestLogEntry>();
        }
    }

    private async Task AppendWebhookTestLogAsync(WebhookTestLogEntry entry)
    {
        var list = await ReadWebhookTestLogsAsync();
        list.Insert(0, entry);
        if (list.Count > MaxLogs) list = list.Take(MaxLogs).ToList();
        var json = JsonSerializer.Serialize(list);
        var existing = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == LogKey);
        if (existing == null)
        {
            _db.SystemSettings.Add(new SystemSetting
            {
                Id = Guid.NewGuid().ToString("N"),
                SettingKey = LogKey,
                SettingValue = json,
                Description = "Recent webhook test logs (capped)",
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedBy = User?.Identity?.Name
            });
        }
        else
        {
            existing.SettingValue = json;
            existing.UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            existing.UpdatedBy = User?.Identity?.Name;
            _db.SystemSettings.Update(existing);
        }
        await _db.SaveChangesAsync();
    }
}
