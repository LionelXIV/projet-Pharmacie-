using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Services;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.Purchasing)]
public class PurchaseOrdersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PurchaseService _purchase;

    public PurchaseOrdersController(ApplicationDbContext context, PurchaseService purchase)
    {
        _context = context;
        _purchase = purchase;
    }

    public async Task<IActionResult> Index([FromQuery] PurchaseOrderListFilters? filter)
    {
        filter ??= new PurchaseOrderListFilters();
        var q = _context.PurchaseOrders
            .AsNoTracking()
            .Include(o => o.Supplier)
            .AsQueryable();

        if (filter.SupplierId > 0)
            q = q.Where(o => o.SupplierId == filter.SupplierId);

        if (filter.Status.HasValue)
            q = q.Where(o => o.Status == filter.Status.Value);

        var list = await q
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.Id)
            .ToListAsync();

        var suppliers = await _context.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        return View(new PurchaseOrderIndexPageViewModel
        {
            Filter = filter,
            Orders = list,
            SupplierLookup = suppliers
        });
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var order = await _context.PurchaseOrders
            .Include(o => o.Supplier)
            .Include(o => o.Lines)
            .ThenInclude(l => l.Product)
            .Include(o => o.Receipts)
            .ThenInclude(r => r.Lines)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
            return NotFound();

        return View(order);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateSuppliersAsync();
        await PopulateProductsAsync();
        var vm = new PurchaseOrderCreateViewModel
        {
            Lines = Enumerable.Range(0, 8).Select(_ => new PurchaseOrderLineSlotViewModel()).ToList()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PurchaseOrderCreateViewModel model)
    {
        var slots = model.Lines ?? new List<PurchaseOrderLineSlotViewModel>();
        var lines = slots
            .Where(l => l.ProductId > 0 && l.QuantityOrdered > 0)
            .Select(l => (l.ProductId, l.QuantityOrdered))
            .ToList();

        if (lines.Count == 0)
            ModelState.AddModelError(string.Empty, "Ajoutez au moins une ligne avec un produit et une quantité.");

        if (ModelState.IsValid)
        {
            var (ok, error) = await _purchase.CreateOrderAsync(
                model.SupplierId,
                model.OrderDate,
                model.Notes,
                lines);
            if (ok)
                return RedirectToAction(nameof(Index));

            ModelState.AddModelError(string.Empty, error ?? "Enregistrement impossible.");
        }

        model.Lines ??= new List<PurchaseOrderLineSlotViewModel>();
        while (model.Lines.Count < 8)
            model.Lines.Add(new PurchaseOrderLineSlotViewModel());

        await PopulateSuppliersAsync(model.SupplierId);
        await PopulateProductsAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var (ok, error) = await _purchase.CancelOrderAsync(id);
        if (!ok)
            TempData["Error"] = error;
        else
            TempData["Message"] = "Commande annulée.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateSuppliersAsync(int? selectedId = null)
    {
        var suppliers = await _context.Suppliers.OrderBy(s => s.Name).ToListAsync();
        ViewData["SupplierId"] = new SelectList(suppliers, "Id", "Name", selectedId);
    }

    private async Task PopulateProductsAsync()
    {
        var products = await _context.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.CommercialName)
            .ToListAsync();
        ViewData["ProductOptions"] = new SelectList(products, "Id", "CommercialName");
    }

}
