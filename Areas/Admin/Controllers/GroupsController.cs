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
public class GroupsController : Controller
{
    private readonly ARCompletionsContext _db;
    private readonly ARCompletions.Services.VendorScopeService _vendorScope;

    public GroupsController(ARCompletionsContext db, ARCompletions.Services.VendorScopeService vendorScope)
    {
        _db = db;
        _vendorScope = vendorScope;
    }

    // GET: Admin/Groups
    public async Task<IActionResult> Index(string? vendorId = null)
    {
        var vendors = await _db.Vendors.ToListAsync();
        ViewBag.Vendors = vendors;

        // NOTE: Currently no LineGroup table exists in domain.
        // This scaffold returns an empty list. Implement DB-backed retrieval later.
        var items = new List<object>();
        return View(items);
    }

    // GET: Admin/Groups/Details/5
    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        // TODO: Load group by id from DB when model/table available.
        await Task.CompletedTask;
        var model = new { Id = id, Name = "(未實作)" };
        return View(model);
    }

    // GET: Admin/Groups/Create
    public IActionResult Create()
    {
        // Show create form. Persistence not implemented in scaffold.
        return View(new { Name = string.Empty, BotEnabled = false });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, bool botEnabled)
    {
        // TODO: Persist new group to DB. Currently not implemented.
        await Task.CompletedTask;
        TempData["Success"] = "群組已建立";
        return RedirectToAction(nameof(Index));
    }

    // GET: Admin/Groups/Edit/5
    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();
        // TODO: Load group for edit. Placeholder model returned.
        await Task.CompletedTask;
        var model = new { Id = id, Name = "(未實作)", BotEnabled = false };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, string name, bool botEnabled)
    {
        // TODO: Persist edit to DB. Currently not implemented.
        await Task.CompletedTask;
        TempData["Success"] = "群組已更新";
        return RedirectToAction(nameof(Index));
    }
}
