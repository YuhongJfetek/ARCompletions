using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using ARCompletions.Data;
using ARCompletions.Domain;
using ARCompletions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ARCompletions.Areas.Vendor.Controllers
{
    [Area("Vendor")]
    [Authorize(Policy = "Vendor")]
    public class FaqCandidatesBatchController : Controller
    {
        private readonly ARCompletionsContext _db;
        private readonly IBackgroundJobQueue _jobQueue;

        public FaqCandidatesBatchController(ARCompletionsContext db, IBackgroundJobQueue jobQueue)
        {
            _db = db;
            _jobQueue = jobQueue;
        }

        private string? GetVendorId() => User.FindFirst("VendorId")?.Value;

        public async Task<IActionResult> Index()
        {
            var vendorId = GetVendorId();
            if (vendorId == null) return Forbid();

            var items = await _db.FaqCandidates
                .Where(c => c.VendorId == vendorId)
                .OrderByDescending(c => c.GeneratedAt)
                .Take(200)
                .ToListAsync();

            var categories = await _db.FaqCategories
                .Where(c => c.VendorId == vendorId && c.IsActive)
                .OrderBy(c => c.Sort)
                .ToListAsync();

            var vm = new ARCompletions.Areas.Vendor.Models.VendorFaqCandidatesBatchViewModel
            {
                Items = items,
                Categories = categories.Select(c => new SelectListItem(c.Name, c.Id)).ToList(),
                SelectedCategoryId = categories.FirstOrDefault()?.Id
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchCreate(string[]? selectedIds, string? categoryId)
        {
            var vendorId = GetVendorId();
            if (vendorId == null) return Forbid();

            if (selectedIds == null || selectedIds.Length == 0)
            {
                ModelState.AddModelError("SelectedIds", "Please select at least one candidate.");
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrEmpty(categoryId))
            {
                ModelState.AddModelError("CategoryId", "Please select a target category.");
                return RedirectToAction(nameof(Index));
            }

            var candidates = await _db.FaqCandidates
                .Where(c => c.VendorId == vendorId && selectedIds.Contains(c.Id))
                .ToListAsync();

            if (candidates.Count == 0) return BadRequest("No valid candidates found.");

            var createdByType = "Vendor";
            var createdById = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var faqs = new List<Faq>();
            var logs = new List<FaqLog>();

            foreach (var c in candidates)
            {
                var faq = new Faq
                {
                    Id = Guid.NewGuid().ToString(),
                    VendorId = vendorId,
                    CategoryId = categoryId,
                    Question = c.Question,
                    Answer = c.Answer,
                    Tags = c.SuggestedTags,
                    Status = "active",
                    Priority = 0,
                    SourceCandidateId = c.Id,
                    Version = 1,
                    IsActive = true,
                    CreatedByType = createdByType,
                    CreatedById = createdById,
                    CreatedAt = now
                };

                faqs.Add(faq);

                var afterJson = JsonSerializer.Serialize(faq);
                logs.Add(new FaqLog
                {
                    Id = Guid.NewGuid().ToString(),
                    VendorId = vendorId,
                    FaqId = faq.Id,
                    ActionType = "CreateFromCandidate",
                    BeforeJson = null,
                    AfterJson = afterJson,
                    OperatedByType = createdByType,
                    OperatedById = createdById,
                    OperatedAt = now
                });

                // mark candidate converted
                c.Status = "converted";
                c.ReviewedByType = createdByType;
                c.ReviewedById = createdById;
                c.ReviewedAt = now;
            }

            await _db.Faqs.AddRangeAsync(faqs);
            await _db.FaqLogs.AddRangeAsync(logs);
            await _db.SaveChangesAsync();

            // create an embedding job to process the new FAQs
            var job = new EmbeddingJob
            {
                Id = Guid.NewGuid().ToString(),
                VendorId = vendorId,
                JobNo = $"BATCH-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
                Status = "queued",
                TriggerType = "Batch",
                TotalFaqCount = faqs.Count,
                SuccessCount = 0,
                FailCount = 0,
                VectorVersion = "v1",
                ModelName = "default",
                CreatedAt = now
            };

            _db.EmbeddingJobs.Add(job);
            await _db.SaveChangesAsync();

            // enqueue for background processing
            _jobQueue.Enqueue(job.Id);

            // provide feedback to the Index view: count, ids, and friendly titles (JSON)
            TempData["BatchCreatedCount"] = faqs.Count.ToString();
            TempData["BatchCreatedIds"] = string.Join(',', faqs.Select(f => f.Id));
            try
            {
                TempData["BatchCreatedList"] = System.Text.Json.JsonSerializer.Serialize(faqs.Select(f => new { f.Id, f.Question }));
            }
            catch { }

            return RedirectToAction("Index", "FaqCandidates");
        }
    }
}
