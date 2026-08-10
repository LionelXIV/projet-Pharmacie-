using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Services;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.GoodsReceipt)]
public class GoodsReceiptsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PurchaseService _purchase;
    private readonly InventoryService _inventory;
    private readonly BlImportService _blImport;

    public GoodsReceiptsController(
        ApplicationDbContext context,
        PurchaseService purchase,
        InventoryService inventory,
        BlImportService blImport)
    {
        _context = context;
        _purchase = purchase;
        _inventory = inventory;
        _blImport = blImport;
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
            .Include(r => r.Supplier)
            .Include(r => r.PurchaseOrder!)
            .ThenInclude(o => o.Supplier)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchNumber))
        {
            var term = searchNumber.Trim();
            query = query.Where(r =>
                r.Id.ToString().Contains(term)
                || (r.Reference != null && r.Reference.Contains(term))
                || (r.Notes != null && r.Notes.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(searchSupplier))
        {
            var term = searchSupplier.Trim().ToLower();
            query = query.Where(r =>
                (r.Supplier != null && r.Supplier.Name.ToLower().Contains(term))
                || (r.PurchaseOrder != null
                    && r.PurchaseOrder.Supplier != null
                    && r.PurchaseOrder.Supplier.Name.ToLower().Contains(term)));
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

    [HttpGet]
    public async Task<IActionResult> CreateDirect()
    {
        ViewBag.Fournisseurs = await _context.Suppliers
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync();
        return View(new GoodsReceiptDirectViewModel());
    }

    /// <summary>Prévisualisation import BL (xlsx/csv) — ne sauvegarde pas, préremplit le formulaire.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> ImportBlPreview(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return Json(new { ok = false, message = "Choisissez un fichier .xlsx, .csv ou .pdf." });

        await using var stream = file.OpenReadStream();
        var result = await _blImport.PreviewAsync(stream, file.FileName, cancellationToken);
        return Json(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDirect(GoodsReceiptDirectViewModel model)
    {
        ViewBag.Fournisseurs = await _context.Suppliers
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync();

        model.Lignes ??= new List<GoodsReceiptDirectLigne>();
        var lignes = model.Lignes
            .Where(l => l.ProductId > 0 && l.QuantiteLivree > 0)
            .ToList();

        if (lignes.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Ajoutez au moins une ligne avec produit et quantité.");
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var displayName = User.Identity?.Name
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? "";
        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var receipt = new GoodsReceipt
            {
                PurchaseOrderId = null,
                SupplierId = model.SupplierId,
                Reference = string.IsNullOrWhiteSpace(model.Reference) ? null : model.Reference.Trim(),
                ReceivedAt = model.DateReception == default ? DateTime.Now : model.DateReception,
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim()
            };
            _context.GoodsReceipts.Add(receipt);
            await _context.SaveChangesAsync();

            var enfantsCrees = 0;
            foreach (var ligne in lignes)
            {
                var product = await _context.Products
                    .Include(p => p.ChildProducts)
                    .FirstOrDefaultAsync(p => p.Id == ligne.ProductId);
                if (product == null)
                    continue;

                if (product.ParentProductId.HasValue)
                {
                    await tx.RollbackAsync();
                    ModelState.AddModelError(string.Empty,
                        $"« {product.CommercialName} » est un produit unité — réceptionnez la boîte parente.");
                    return View(model);
                }

                if (ligne.CreerVenteDetail
                    && !product.EstVenteDetail
                    && !product.ChildProducts.Any()
                    && ligne.NbUnitesParBoite is > 0
                    && ligne.PrixUnite is > 0)
                {
                    await CreateChildProductInternalAsync(
                        product,
                        ligne.NbUnitesParBoite.Value,
                        ligne.PrixUnite.Value);
                    enfantsCrees++;
                }

                if (ligne.PrixAchat > 0 && !ligne.EstUG && ligne.PrixAchat != product.PurchasePrice)
                    product.PurchasePrice = ligne.PrixAchat;

                if (ligne.PrixVente > 0 && ligne.PrixVente != product.SalePrice)
                {
                    _context.PrixModifications.Add(new PrixModification
                    {
                        ProductId = product.Id,
                        AncienPrix = product.SalePrice,
                        NouveauPrix = ligne.PrixVente,
                        ModifiedAt = DateTime.Now,
                        ModifiedByUserId = userId ?? "",
                        ModifiedByDisplayName = displayName,
                        Raison = string.IsNullOrWhiteSpace(model.Reference)
                            ? $"Mise à jour via BL Direct #{receipt.Id}"
                            : $"Mise à jour via BL {model.Reference.Trim()}"
                    });
                    product.SalePrice = ligne.PrixVente;
                }

                var lotNumber = string.IsNullOrWhiteSpace(ligne.NumeroLot)
                    ? $"BL-{receipt.Id}-{ligne.ProductId}-{DateTime.Now:HHmmss}"
                    : ligne.NumeroLot.Trim();
                var expiration = (ligne.DatePeremption ?? DateTime.Today.AddYears(2)).Date;
                var reason = $"BL Direct #{receipt.Id}"
                    + (string.IsNullOrWhiteSpace(receipt.Reference) ? "" : $" — {receipt.Reference}");

                var (ok, err, batch) = await _inventory.StageEntreeAsync(
                    product.Id,
                    lotNumber,
                    expiration,
                    ligne.QuantiteLivree,
                    reason,
                    userId);
                if (!ok || batch == null)
                {
                    await tx.RollbackAsync();
                    ModelState.AddModelError(string.Empty, err ?? "Entrée stock impossible.");
                    return View(model);
                }

                if (ligne.NbBoitesAOuvrir > 0)
                {
                    var openError = await TryOpenBoxesOnReceiptAsync(
                        product,
                        batch,
                        ligne.NbBoitesAOuvrir,
                        userId);
                    if (openError != null)
                    {
                        await tx.RollbackAsync();
                        ModelState.AddModelError(string.Empty, openError);
                        return View(model);
                    }
                }

                _context.GoodsReceiptLines.Add(new GoodsReceiptLine
                {
                    GoodsReceiptId = receipt.Id,
                    PurchaseOrderLineId = null,
                    ProductId = product.Id,
                    QuantityReceived = ligne.QuantiteLivree,
                    LotNumber = lotNumber,
                    ExpirationDate = expiration
                });
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["Success"] = enfantsCrees > 0
                ? $"BL saisi avec succès. Stock mis à jour. {enfantsCrees} produit(s) unité créé(s)."
                : "BL saisi avec succès. Stock mis à jour.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    private async Task CreateChildProductInternalAsync(Product parent, int nbUnitesParBoite, decimal prixUnite)
    {
        var enfant = new Product
        {
            CommercialName = parent.CommercialName + " — Unité",
            GenericName = parent.GenericName,
            SalePrice = prixUnite,
            PurchasePrice = nbUnitesParBoite > 0
                ? Math.Round(parent.PurchasePrice / nbUnitesParBoite, 2)
                : 0,
            StockQuantity = 0,
            AlertThreshold = 0,
            CategoryId = parent.CategoryId,
            SupplierId = parent.SupplierId,
            IsActive = true,
            ParentProductId = parent.Id,
            NbUnitesParBoite = nbUnitesParBoite,
            TarifType = parent.TarifType,
            ProductType = parent.ProductType,
            Form = parent.Form,
            Dosage = parent.Dosage
        };
        TVACalculator.AppliquerTarif(enfant);
        parent.EstVenteDetail = true;
        _context.Products.Add(enfant);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Convertit immédiatement des boîtes du lot reçu en unités (tablettes) sur le produit enfant.
    /// </summary>
    private async Task<string?> TryOpenBoxesOnReceiptAsync(
        Product parent,
        ProductBatch lotParent,
        int nbBoitesAOuvrir,
        string? userId)
    {
        if (nbBoitesAOuvrir <= 0)
            return null;

        if (nbBoitesAOuvrir > lotParent.Quantity)
        {
            return $"Impossible d'ouvrir {nbBoitesAOuvrir} boîte(s) pour « {parent.CommercialName} » : " +
                   $"seulement {lotParent.Quantity} reçue(s) sur ce lot.";
        }

        var enfant = await _context.Products
            .FirstOrDefaultAsync(p => p.ParentProductId == parent.Id);
        if (enfant == null || enfant.NbUnitesParBoite is not > 0)
        {
            return $"« {parent.CommercialName} » n'a pas de produit tablette (vente détail) " +
                   "— créez l'unité ou cochez « Créer unité » avant d'ouvrir des boîtes.";
        }

        var nbTablettes = nbBoitesAOuvrir * enfant.NbUnitesParBoite.Value;
        var now = DateTime.Now;

        lotParent.Quantity -= nbBoitesAOuvrir;
        parent.StockQuantity -= nbBoitesAOuvrir;
        if (parent.StockQuantity < 0)
            parent.StockQuantity = 0;

        var lotEnfant = await _context.ProductBatches
            .FirstOrDefaultAsync(b =>
                b.ProductId == enfant.Id
                && b.LotNumber == lotParent.LotNumber
                && b.ExpirationDate.Date == lotParent.ExpirationDate.Date);

        if (lotEnfant == null)
        {
            lotEnfant = new ProductBatch
            {
                ProductId = enfant.Id,
                LotNumber = lotParent.LotNumber,
                ExpirationDate = lotParent.ExpirationDate.Date,
                Quantity = nbTablettes
            };
            _context.ProductBatches.Add(lotEnfant);
        }
        else
        {
            lotEnfant.Quantity += nbTablettes;
        }

        enfant.StockQuantity += nbTablettes;

        _context.StockMovements.Add(new StockMovement
        {
            ProductId = parent.Id,
            Batch = lotParent,
            Type = StockMovementType.Sortie,
            Quantity = nbBoitesAOuvrir,
            OccurredAt = now,
            UserId = userId,
            Reason = $"Ouverture boîte → {nbTablettes} tablettes (BL Direct)"
        });

        _context.StockMovements.Add(new StockMovement
        {
            ProductId = enfant.Id,
            Batch = lotEnfant,
            Type = StockMovementType.Entree,
            Quantity = nbTablettes,
            OccurredAt = now,
            UserId = userId,
            Reason = $"Ouverture boîte ← {nbBoitesAOuvrir} boîte(s) de {parent.CommercialName} (BL Direct)"
        });

        return null;
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
