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
public class BatchesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly InventoryService _inventory;

    public BatchesController(ApplicationDbContext context, InventoryService inventory)
    {
        _context = context;
        _inventory = inventory;
    }

    public async Task<IActionResult> Index([FromQuery] BatchListFilters? filter)
    {
        filter ??= new BatchListFilters();
        var today = DateTime.Today;
        var horizonEnd = today.AddDays(AlertsIndexViewModel.ExpirationHorizonDays);

        var q = _context.ProductBatches
            .AsNoTracking()
            .Include(b => b.Product)
            .AsQueryable();

        if (filter.ProductId > 0)
            q = q.Where(b => b.ProductId == filter.ProductId);

        if (!string.IsNullOrWhiteSpace(filter.Lot))
        {
            var lot = filter.Lot.Trim();
            q = q.Where(b => b.LotNumber.Contains(lot));
        }

        if (filter.Expiration == "near")
        {
            q = q.Where(b =>
                b.Quantity > 0
                && b.ExpirationDate.Date >= today
                && b.ExpirationDate.Date <= horizonEnd);
        }
        else if (filter.Expiration == "expired")
        {
            q = q.Where(b => b.Quantity > 0 && b.ExpirationDate.Date < today);
        }

        var list = await q
            .OrderBy(b => b.ExpirationDate)
            .ThenBy(b => b.Product!.CommercialName)
            .ToListAsync();

        var products = await _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.CommercialName)
            .ToListAsync();

        return View(new BatchIndexPageViewModel
        {
            Filter = filter,
            Batches = list,
            ProductLookup = products
        });
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var batch = await _context.ProductBatches
            .Include(b => b.Product)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (batch == null)
            return NotFound();

        var movements = await _context.StockMovements
            .AsNoTracking()
            .Include(m => m.Sale)
            .Where(m => m.BatchId == id)
            .OrderByDescending(m => m.OccurredAt)
            .ToListAsync();
        ViewBag.Movements = movements;
        ViewBag.UserLabels = await UserDisplayResolver.LoadLabelsByIdAsync(_context, movements.Select(m => m.UserId));
        return View(batch);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateProductsAsync();
        return View(new BatchCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BatchCreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var (ok, error) = await _inventory.RecordEntreeAsync(
                model.ProductId,
                model.LotNumber,
                model.ExpirationDate,
                model.Quantity,
                model.Reason,
                userId);
            if (ok)
                return RedirectToAction(nameof(Index));

            ModelState.AddModelError(string.Empty, error ?? "Enregistrement impossible.");
        }

        await PopulateProductsAsync(model.ProductId);
        return View(model);
    }

    private async Task PopulateProductsAsync(int? selectedProductId = null)
    {
        var products = await _context.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.CommercialName)
            .ToListAsync();
        ViewData["ProductId"] = new SelectList(products, "Id", "CommercialName", selectedProductId);
    }

}
