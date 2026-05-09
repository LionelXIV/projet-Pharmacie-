using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Services;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.Inventory)]
public class StockMovementsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly InventoryService _inventory;

    public StockMovementsController(ApplicationDbContext context, InventoryService inventory)
    {
        _context = context;
        _inventory = inventory;
    }

    public async Task<IActionResult> Index([FromQuery] StockMovementListFilters? filter)
    {
        filter ??= new StockMovementListFilters();
        var q = _context.StockMovements
            .AsNoTracking()
            .Include(m => m.Product)
            .Include(m => m.Batch)
            .Include(m => m.Sale)
            .AsQueryable();

        if (filter.ProductId > 0)
            q = q.Where(m => m.ProductId == filter.ProductId);

        if (filter.Type.HasValue)
            q = q.Where(m => m.Type == filter.Type.Value);

        if (filter.From.HasValue)
            q = q.Where(m => m.OccurredAt >= filter.From.Value.Date);

        if (filter.To.HasValue)
            q = q.Where(m => m.OccurredAt < filter.To.Value.Date.AddDays(1));

        if (!string.IsNullOrEmpty(filter.UserId))
            q = q.Where(m => m.UserId == filter.UserId);

        var list = await q
            .OrderByDescending(m => m.OccurredAt)
            .ThenByDescending(m => m.Id)
            .ToListAsync();

        ViewBag.UserLabels = await UserDisplayResolver.LoadLabelsByIdAsync(_context, list.Select(m => m.UserId));

        var products = await _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.CommercialName)
            .ToListAsync();

        await PopulateMovementUserFilterAsync(filter.UserId);
        ViewData["FilterType"] = new SelectList(
            new[]
            {
                new { Value = "", Text = "Tous les types" },
                new { Value = ((int)StockMovementType.Entree).ToString(), Text = "Entrée" },
                new { Value = ((int)StockMovementType.Sortie).ToString(), Text = "Sortie" },
                new { Value = ((int)StockMovementType.Ajustement).ToString(), Text = "Ajustement" }
            },
            "Value",
            "Text",
            filter.Type.HasValue ? ((int)filter.Type.Value).ToString() : "");

        return View(new StockMovementIndexPageViewModel
        {
            Filter = filter,
            Movements = list,
            ProductLookup = products
        });
    }

    public async Task<IActionResult> Sortie()
    {
        await PopulateBatchesAsync(onlyPositiveQty: true);
        return View(new StockSortieViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sortie(StockSortieViewModel model)
    {
        if (ModelState.IsValid)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (ok, error) = await _inventory.RecordSortieAsync(
                model.BatchId,
                model.Quantity,
                model.Reason,
                userId);
            if (ok)
                return RedirectToAction(nameof(Index));

            ModelState.AddModelError(string.Empty, error ?? "Sortie impossible.");
        }

        await PopulateBatchesAsync(onlyPositiveQty: true, selectedBatchId: model.BatchId);
        return View(model);
    }

    public async Task<IActionResult> Ajustement()
    {
        await PopulateBatchesAsync(onlyPositiveQty: false);
        return View(new StockAjustementViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ajustement(StockAjustementViewModel model)
    {
        if (ModelState.IsValid)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (ok, error) = await _inventory.RecordAjustementAsync(
                model.BatchId,
                model.Delta,
                model.Reason,
                userId);
            if (ok)
                return RedirectToAction(nameof(Index));

            ModelState.AddModelError(string.Empty, error ?? "Ajustement impossible.");
        }

        await PopulateBatchesAsync(onlyPositiveQty: false, selectedBatchId: model.BatchId);
        return View(model);
    }

    private async Task PopulateBatchesAsync(bool onlyPositiveQty, int? selectedBatchId = null)
    {
        var q = _context.ProductBatches
            .Include(b => b.Product)
            .AsQueryable();
        if (onlyPositiveQty)
            q = q.Where(b => b.Quantity > 0);

        var list = await q
            .OrderBy(b => b.Product!.CommercialName)
            .ThenBy(b => b.LotNumber)
            .ToListAsync();

        var items = list.Select(b => new
        {
            b.Id,
            Label = $"{b.Product!.CommercialName} — Lot {b.LotNumber} — Dispo: {b.Quantity} — Exp. {b.ExpirationDate:yyyy-MM-dd}"
        }).ToList();

        ViewData["BatchId"] = new SelectList(items, "Id", "Label", selectedBatchId);
    }

    private async Task PopulateMovementUserFilterAsync(string? selectedUserId)
    {
        var userIds = await _context.StockMovements
            .AsNoTracking()
            .Where(m => m.UserId != null && m.UserId != "")
            .Select(m => m.UserId!)
            .Distinct()
            .ToListAsync();

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .OrderBy(u => u.Email)
            .ThenBy(u => u.UserName)
            .ToListAsync();

        var userItems = users
            .Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = UserDisplayResolver.Format(u.Email, u.UserName),
                Selected = u.Id == selectedUserId
            })
            .ToList();
        ViewData["FilterUserId"] = userItems;
    }
}
