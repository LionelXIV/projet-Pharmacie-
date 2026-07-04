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
