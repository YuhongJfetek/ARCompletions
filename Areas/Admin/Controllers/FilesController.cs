using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ARCompletions.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace ARCompletions.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = "Platform")]
public class FilesController : Controller
{
    private readonly ARCompletionsContext _db;

    public FilesController(ARCompletionsContext db)
    {
        _db = db;
    }

    // GET: Admin/Files
    public async Task<IActionResult> Index(string? vendorId = null)
    {
        // TODO: Replace with real file list from DB when model exists (e.g., line_uploaded_files)
        var items = new List<object>();
        return View(items);
    }

    // GET: Admin/Files/Details/5
    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        // TODO: Load file metadata from DB when available
        await Task.CompletedTask;
        var model = new { Id = id, FileName = "(未實作)", Status = "unknown" };
        return View(model);
    }

    // GET: Admin/Files/Validate/5
    public async Task<IActionResult> Validate(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        // Placeholder: show validate UI. Actual validation handled by background job/service.
        await Task.CompletedTask;
        var model = new { Id = id, FileName = "(未實作)", Validated = false };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(string id)
    {
        // TODO: Trigger archive job for file. Not implemented in scaffold.
        await Task.CompletedTask;
        TempData["Success"] = "檔案已標記為歸檔";
        return RedirectToAction(nameof(Index));
    }
}
