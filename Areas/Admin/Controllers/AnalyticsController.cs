using System.Linq;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class AnalyticsController : Controller
{
    private readonly ARCompletionsContext _db;

    public AnalyticsController(ARCompletionsContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        // Conversations
        var totalConvos = await _db.Conversations.LongCountAsync();
        var totalMessages = await _db.ConversationMessages.LongCountAsync();

        // Average confidence (only messages with confidence)
        var confStats = await _db.ConversationMessages
            .Where(m => m.ConfidenceScore != null)
            .GroupBy(m => 1)
            .Select(g => new {
                Avg = g.Average(m => m.ConfidenceScore),
                Min = g.Min(m => m.ConfidenceScore),
                Max = g.Max(m => m.ConfidenceScore)
            }).FirstOrDefaultAsync();

        // Low-confidence count (< 0.5)
        var lowCount = await _db.ConversationMessages.LongCountAsync(m => m.ConfidenceScore == null || m.ConfidenceScore < 0.5);

        // Embedding jobs summary
        var totalEmbedJobs = await _db.EmbeddingJobs.LongCountAsync();
        var recentJobs = await _db.EmbeddingJobs.OrderByDescending(j => j.CreatedAt).Take(10).ToListAsync();

        // Persona settings (stored in SystemSettings with key prefix Persona:)
        var personaSettings = await _db.SystemSettings
            .Where(s => s.SettingKey.StartsWith("Persona:"))
            .ToListAsync();

        ViewBag.TotalConversations = totalConvos;
        ViewBag.TotalMessages = totalMessages;
        ViewBag.ConfidenceStats = confStats ?? new { Avg = (double?)null, Min = (double?)null, Max = (double?)null };
        ViewBag.LowConfidenceCount = lowCount;
        ViewBag.TotalEmbedJobs = totalEmbedJobs;
        ViewBag.RecentEmbedJobs = recentJobs;
        ViewBag.PersonaSettings = personaSettings;

        return View();
    }

    public async Task<IActionResult> Edit(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return NotFound();
        var settingKey = "Persona:" + key;
        var existing = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == settingKey);
        if (existing == null) return NotFound();
        return View(existing);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePersona(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return BadRequest();
        var settingKey = "Persona:" + key;
        var existing = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == settingKey);
        if (existing != null)
        {
            _db.SystemSettings.Remove(existing);
            await _db.SaveChangesAsync();
        }
        TempData["Success"] = "已移除 Persona 設定";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePersona(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key)) return BadRequest();

        var settingKey = "Persona:" + key;
        var existing = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == settingKey);
        if (existing == null)
        {
            _db.SystemSettings.Add(new ARCompletions.Domain.SystemSetting
            {
                Id = System.Guid.NewGuid().ToString("N"),
                SettingKey = settingKey,
                SettingValue = value ?? string.Empty,
                Description = null,
                UpdatedAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedBy = User?.Identity?.Name
            });
        }
        else
        {
            existing.SettingValue = value ?? string.Empty;
            existing.UpdatedAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            existing.UpdatedBy = User?.Identity?.Name;
            _db.SystemSettings.Update(existing);
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Persona 已儲存";
        return RedirectToAction(nameof(Index));
    }
}
