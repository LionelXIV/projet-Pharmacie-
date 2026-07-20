using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Services;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.Purchasing)]
public class GoodsReceiptsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PurchaseService _purchase;

    public GoodsReceiptsController(ApplicationDbContext context, PurchaseService purchase)
    {
        _context = context;
        _purchase = purchase;
    }

    public async Task<IActionResult> Index(
        string? searchNumber,
        string? searchSupplier,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page = 1)
    {
        const int pageSize = 50;
        if (page < 1)
            page = 1;

        var query = _context.GoodsReceipts
            .AsNoTracking()
            .Include(r => r.Lines)
            .Include(r => r.PurchaseOrder!)
            .ThenInclude(o => o.Supplier)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchNumber))
        {
            var term = searchNumber.Trim();
            query = query.Where(r =>
                r.Id.ToString().Contains(term)
                || (r.Notes != null && r.Notes.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(searchSupplier))
        {
            var term = searchSupplier.Trim().ToLower();
            query = query.Where(r =>
                r.PurchaseOrder != null
                && r.PurchaseOrder.Supplier != null
                && r.PurchaseOrder.Supplier.Name.ToLower().Contains(term));
        }

        if (dateFrom.HasValue)
        {
            var from = dateFrom.Value.Date;
            query = query.Where(r => r.ReceivedAt >= from);
        }

        if (dateTo.HasValue)
        {
            var toExclusive = dateTo.Value.Date.AddDays(1);
            query = query.Where(r => r.ReceivedAt < toExclusive);
        }

        var total = await query.CountAsync();
        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)pageSize);
        if (page > totalPages)
            page = totalPages;

        var receipts = await query
            .OrderByDescending(r => r.ReceivedAt)
            .ThenByDescending(r => r.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.SearchNumber = searchNumber;
        ViewBag.SearchSupplier = searchSupplier;
        ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
        ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = total;

        var paginationRoutes = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(searchNumber))
            paginationRoutes["searchNumber"] = searchNumber.Trim();
        if (!string.IsNullOrWhiteSpace(searchSupplier))
            paginationRoutes["searchSupplier"] = searchSupplier.Trim();
        if (dateFrom.HasValue)
            paginationRoutes["dateFrom"] = dateFrom.Value.ToString("yyyy-MM-dd");
        if (dateTo.HasValue)
            paginationRoutes["dateTo"] = dateTo.Value.ToString("yyyy-MM-dd");
        ViewBag.PaginationRoutes = paginationRoutes;

        return View(receipts);
    }

    public async Task<IActionResult> Create(int purchaseOrderId)
    {
        var order = await _context.PurchaseOrders
            .Include(o => o.Lines)
            .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.Id == purchaseOrderId);
        if (order == null)
            return NotFound();
        if (order.Status == PurchaseOrderStatus.Brouillon)
        {
            TempData["Error"] =
                "Impossible de réceptionner une commande en brouillon. " +
                "Envoyez-la d'abord au fournisseur via la page de détail.";
            return RedirectToAction(
                nameof(PurchaseOrdersController.Details),
                "PurchaseOrders",
                new { id = order.Id });
        }
        if (order.Status == PurchaseOrderStatus.Annulee || order.Status == PurchaseOrderStatus.Recue)
        {
            TempData["Error"] = "Réception impossible pour cette commande.";
            return RedirectToAction(nameof(PurchaseOrdersController.Details), "PurchaseOrders", new { id = purchaseOrderId });
        }

        var openLines = order.Lines
            .Where(l => l.QuantityOrdered > l.QuantityReceived)
            .OrderBy(l => l.Id)
            .ToList();
        if (openLines.Count == 0)
        {
            TempData["Error"] = "Toutes les lignes sont déjà entièrement reçues.";
            return RedirectToAction(nameof(PurchaseOrdersController.Details), "PurchaseOrders", new { id = purchaseOrderId });
        }

        var vm = BuildReceptionViewModel(order, openLines);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReceptionFormViewModel model)
    {
        var order = await _context.PurchaseOrders
            .FirstOrDefaultAsync(o => o.Id == model.PurchaseOrderId);

        if (order == null)
            return NotFound();

        if (order.Status == PurchaseOrderStatus.Brouillon)
        {
            TempData["Error"] = "Impossible de réceptionner une commande en brouillon.";
            return RedirectToAction(
                nameof(PurchaseOrdersController.Details),
                "PurchaseOrders",
                new { id = model.PurchaseOrderId });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var (ok, error) = await _purchase.RecordReceptionAsync(model.PurchaseOrderId, model, userId);
        if (ok)
        {
            TempData["Success"] = "Réception enregistrée.";
            return RedirectToAction(nameof(PurchaseOrdersController.Details), "PurchaseOrders",
                new { id = model.PurchaseOrderId });
        }

        TempData["Error"] = error ?? "Réception impossible.";
        return RedirectToAction(nameof(Create), new { purchaseOrderId = model.PurchaseOrderId });
    }

    private static ReceptionFormViewModel BuildReceptionViewModel(
        PurchaseOrder order,
        IReadOnlyList<PurchaseOrderLine> openLines)
    {
        return new ReceptionFormViewModel
        {
            PurchaseOrderId = order.Id,
            Lines = openLines.Select(l => new ReceptionLineRowViewModel
            {
                PurchaseOrderLineId = l.Id,
                ProductName = l.Product?.CommercialName ?? $"Produit #{l.ProductId}",
                QuantityOrdered = l.QuantityOrdered,
                QuantityReceivedBefore = l.QuantityReceived,
                QuantityReceived = 0,
                LotNumber = null,
                ExpirationDate = null
            }).ToList()
        };
    }
}
