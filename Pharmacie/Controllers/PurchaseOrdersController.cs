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

    public async Task<IActionResult> Index([FromQuery] PurchaseOrderListFilters? filter, int page = 1)
    {
        filter ??= new PurchaseOrderListFilters();
        const int pageSize = 50;
        if (page < 1)
            page = 1;

        var query = _context.PurchaseOrders
            .AsNoTracking()
            .AsQueryable();

        if (filter.SupplierId > 0)
            query = query.Where(o => o.SupplierId == filter.SupplierId);

        if (filter.Status.HasValue)
            query = query.Where(o => o.Status == filter.Status.Value);

        int total = await query.CountAsync();
        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)pageSize);
        if (page > totalPages)
            page = totalPages;

        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(o => o.Supplier)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = total;

        var paginationRoutes = new Dictionary<string, string>();
        if (filter.SupplierId > 0)
            paginationRoutes["SupplierId"] = filter.SupplierId.Value.ToString();
        if (filter.Status.HasValue)
            paginationRoutes["Status"] = ((int)filter.Status.Value).ToString();
        ViewBag.PaginationRoutes = paginationRoutes;

        var suppliers = await _context.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        return View(new PurchaseOrderIndexPageViewModel
        {
            Filter = filter,
            Orders = orders,
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
        var vm = new PurchaseOrderCreateViewModel
        {
            Lines = Enumerable.Range(0, 8).Select(_ => new PurchaseOrderLineSlotViewModel()).ToList()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PurchaseOrderCreateViewModel model, string submitAction = "send")
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
            var asDraft = submitAction == "draft";
            var (ok, error) = await _purchase.CreateOrderAsync(
                model.SupplierId,
                model.OrderDate,
                model.Notes,
                lines,
                asDraft);
            if (ok)
            {
                TempData["Success"] = asDraft
                    ? "Commande enregistrée en brouillon."
                    : "Commande créée et envoyée au fournisseur.";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError(string.Empty, error ?? "Enregistrement impossible.");
        }

        model.Lines ??= new List<PurchaseOrderLineSlotViewModel>();
        while (model.Lines.Count < 8)
            model.Lines.Add(new PurchaseOrderLineSlotViewModel());

        await PopulateSuppliersAsync(model.SupplierId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(int id)
    {
        var order = await _context.PurchaseOrders.FindAsync(id);
        if (order == null)
            return NotFound();

        if (order.Status != PurchaseOrderStatus.Brouillon)
        {
            TempData["Error"] = "Seules les commandes en brouillon peuvent être envoyées.";
            return RedirectToAction(nameof(Details), new { id });
        }

        order.Status = PurchaseOrderStatus.Envoyee;
        await _context.SaveChangesAsync();
        TempData["Success"] = "Commande envoyée au fournisseur.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var (ok, error) = await _purchase.CancelOrderAsync(id);
        if (!ok)
            TempData["Error"] = error;
        else
            TempData["Success"] = "Commande annulée.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateSuppliersAsync(int? selectedId = null)
    {
        var suppliers = await _context.Suppliers.OrderBy(s => s.Name).ToListAsync();
        ViewData["SupplierId"] = new SelectList(suppliers, "Id", "Name", selectedId);
    }

}
