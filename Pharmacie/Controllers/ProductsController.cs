using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Models.Dto;
using Pharmacie.Reporting;
using Pharmacie.Services;

namespace Pharmacie.Controllers;

[Authorize]
public class ProductsController : Controller
{
    private const int IndexPageSize = 50;
    private const int ClassifyPageSize = 50;

    private readonly ApplicationDbContext _context;

    public ProductsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [Authorize(Roles = AppRoles.CatalogRead)]
    public async Task<IActionResult> Index([FromQuery] ProductListFilters? filter, int page = 1)
    {
        filter ??= new ProductListFilters();
        if (page < 1)
            page = 1;

        var q = FilteredProductsQuery(filter);
        var totalCount = await q.CountAsync();
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)IndexPageSize);
        if (page > totalPages)
            page = totalPages;

        var list = await q
            .OrderBy(p => p.CommercialName)
            .Skip((page - 1) * IndexPageSize)
            .Take(IndexPageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;

        var categories = await _context.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        var suppliers = await _context.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        return View(new ProductIndexPageViewModel
        {
            Filter = filter,
            Products = list,
            CategoryLookup = categories,
            SupplierLookup = suppliers
        });
    }

    [HttpGet]
    [Authorize(Policy = "ProductSearch")]
    public async Task<IActionResult> Search(string? term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Trim().Length < 2)
            return Json(Array.Empty<object>());

        var pattern = term.Trim();
        var results = await _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive
                && (p.CommercialName.Contains(pattern)
                    || (p.Cip != null && p.Cip.Contains(pattern))))
            .OrderBy(p => p.CommercialName)
            .Take(25)
            .Select(p => new
            {
                value = p.Id,
                text = (p.Cip != null && p.Cip != ""
                    ? p.Cip + " — " + p.CommercialName
                    : p.CommercialName)
                    + (p.ParentProductId != null
                        ? " 〔tablette〕"
                        : p.EstVenteDetail
                            ? " 〔boîte〕"
                            : "")
                    + " (stock: " + p.StockQuantity + ")",
                salePrice = p.SalePrice,
                purchasePrice = p.PurchasePrice,
                stockQuantity = p.StockQuantity,
                assujettiTVA = p.AssujettiTVA,
                tauxTVA = p.TauxTVA,
                estTablette = p.ParentProductId != null,
                estBoite = p.EstVenteDetail && p.ParentProductId == null,
                nomParent = p.ParentProduct != null ? p.ParentProduct.CommercialName : ""
            })
            .ToListAsync();

        return Json(results);
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.CatalogRead},{AppRoles.GoodsReceipt},{AppRoles.Administrateur}")]
    public async Task<IActionResult> CategoriesJson()
    {
        var list = await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();
        return Json(list);
    }

    [HttpPost]
    [Authorize(Roles = AppRoles.CanModifyPrice)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateQuick([FromForm] QuickProductDto dto)
    {
        var name = dto.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { error = "Le nom du produit est obligatoire." });

        if (dto.SalePrice <= 0)
            return BadRequest(new { error = "Le prix de vente doit être supérieur à 0." });

        if (dto.PurchasePrice < 0)
            return BadRequest(new { error = "Le prix d'achat ne peut pas être négatif." });

        if (dto.CategoryId <= 0)
            return BadRequest(new { error = "Choisissez une catégorie." });

        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId);
        if (!categoryExists)
            return BadRequest(new { error = "Choisissez une catégorie." });

        int supplierId;
        if (dto.SupplierId is > 0
            && await _context.Suppliers.AnyAsync(s => s.Id == dto.SupplierId.Value))
            supplierId = dto.SupplierId.Value;
        else
            supplierId = await GetOrCreateSupplierIdAsync("Fournisseur non précisé");

        var cip = string.IsNullOrWhiteSpace(dto.Cip) ? null : dto.Cip.Trim();

        var product = new Product
        {
            CommercialName = name,
            PurchasePrice = dto.PurchasePrice,
            SalePrice = dto.SalePrice,
            CategoryId = dto.CategoryId,
            SupplierId = supplierId,
            Cip = cip,
            ProductType = ProductType.Inconnu,
            IsActive = true,
            StockQuantity = 0,
            AlertThreshold = 0
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        var text = product.CommercialName + " (stock: 0)";
        return Json(new { id = product.Id, text, value = product.Id, salePrice = product.SalePrice, purchasePrice = product.PurchasePrice });
    }

    private async Task<int> GetOrCreateCategoryIdAsync(string name)
    {
        var existing = await _context.Categories.FirstOrDefaultAsync(c => c.Name == name);
        if (existing != null)
            return existing.Id;

        var category = new Category { Name = name };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        return category.Id;
    }

    private async Task<int> GetOrCreateSupplierIdAsync(string name)
    {
        var existing = await _context.Suppliers.FirstOrDefaultAsync(s => s.Name == name);
        if (existing != null)
            return existing.Id;

        var supplier = new Supplier { Name = name };
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();
        return supplier.Id;
    }

    [Authorize(Roles = AppRoles.CatalogRead)]
    public async Task<IActionResult> IndexCsv([FromQuery] ProductListFilters? filter)
    {
        filter ??= new ProductListFilters();
        var list = await FilteredProductsQuery(filter)
            .OrderBy(p => p.CommercialName)
            .ToListAsync();

        var sb = ReportCsvFormatter.CreateBuilder();
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("N°"),
            ReportCsvFormatter.Escape("Nom commercial"),
            ReportCsvFormatter.Escape("Nom générique"),
            ReportCsvFormatter.Escape("Catégorie"),
            ReportCsvFormatter.Escape("Forme"),
            ReportCsvFormatter.Escape("Dosage"),
            ReportCsvFormatter.Escape("Fournisseur"),
            ReportCsvFormatter.Escape("Prix achat (FCFA)"),
            ReportCsvFormatter.Escape("Prix vente (FCFA)"),
            ReportCsvFormatter.Escape("Stock"),
            ReportCsvFormatter.Escape("Seuil"),
            ReportCsvFormatter.Escape("Emplacement"),
            ReportCsvFormatter.Escape("Actif")));

        foreach (var p in list)
        {
            sb.AppendLine(ReportCsvFormatter.Join(
                ReportCsvFormatter.IntInvariant(p.Id),
                ReportCsvFormatter.Escape(p.CommercialName),
                ReportCsvFormatter.Escape(p.GenericName ?? ""),
                ReportCsvFormatter.Escape(p.Category?.Name ?? ""),
                ReportCsvFormatter.Escape(p.Form ?? ""),
                ReportCsvFormatter.Escape(p.Dosage ?? ""),
                ReportCsvFormatter.Escape(p.Supplier?.Name ?? ""),
                ReportCsvFormatter.FcfaCsvAmount(p.PurchasePrice),
                ReportCsvFormatter.FcfaCsvAmount(p.SalePrice),
                ReportCsvFormatter.IntInvariant(p.StockQuantity),
                ReportCsvFormatter.IntInvariant(p.AlertThreshold),
                ReportCsvFormatter.Escape(p.Location ?? ""),
                p.IsActive ? ReportCsvFormatter.Escape("Oui") : ReportCsvFormatter.Escape("Non")));
        }

        return ReportCsvFormatter.FileResult(this, sb.ToString(), "export-catalogue-produits");
    }

    [Authorize(Roles = AppRoles.CatalogRead)]
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (product == null)
            return NotFound();

        return View(product);
    }

    [Authorize(Roles = AppRoles.CanModifyPrice)]
    public async Task<IActionResult> Create()
    {
        await PopulateLookupsAsync();
        return View(new Product());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.CanModifyPrice)]
    public async Task<IActionResult> Create(
        [Bind(
            "CommercialName,GenericName,CategoryId,Form,Dosage,SupplierId,PurchasePrice,SalePrice,AlertThreshold,Location,IsActive,TarifType")]
        Product product)
    {
        if (ModelState.IsValid)
        {
            product.StockQuantity = 0;
            TVACalculator.AppliquerTarif(product);
            _context.Add(product);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Produit créé.";
            return RedirectToAction(nameof(Index));
        }

        await PopulateLookupsAsync(product.CategoryId, product.SupplierId);
        return View(product);
    }

    [Authorize(Roles = AppRoles.CanModifyPrice)]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var product = await _context.Products
            .Include(p => p.ChildProducts)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (product == null)
            return NotFound();

        ViewBag.Enfant = product.ChildProducts.OrderBy(c => c.Id).FirstOrDefault();
        await PopulateLookupsAsync(product.CategoryId, product.SupplierId);
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.PharmacienTitulaire},{AppRoles.Pharmacien},{AppRoles.Administrateur}")]
    public async Task<IActionResult> CreateEnfant(int parentId, int nbUnitesParBoite, decimal prixUnite)
    {
        if (nbUnitesParBoite < 1 || prixUnite <= 0)
        {
            TempData["Error"] = "Indiquez un nombre d'unités (≥ 1) et un prix unitaire > 0.";
            return RedirectToAction(nameof(Edit), new { id = parentId });
        }

        var parent = await _context.Products
            .Include(p => p.ChildProducts)
            .FirstOrDefaultAsync(p => p.Id == parentId);
        if (parent == null)
            return NotFound();

        if (parent.ParentProductId.HasValue)
        {
            TempData["Error"] = "Impossible de créer une unité sur un produit qui est déjà une unité.";
            return RedirectToAction(nameof(Edit), new { id = parentId });
        }

        if (parent.ChildProducts.Count > 0 || parent.EstVenteDetail)
        {
            TempData["Error"] = "Ce produit a déjà un produit unité associé.";
            return RedirectToAction(nameof(Edit), new { id = parentId });
        }

        var enfant = new Product
        {
            CommercialName = parent.CommercialName + " — Unité",
            GenericName = parent.GenericName,
            SalePrice = prixUnite,
            PurchasePrice = Math.Round(parent.PurchasePrice / nbUnitesParBoite, 2),
            StockQuantity = 0,
            AlertThreshold = 0,
            CategoryId = parent.CategoryId,
            SupplierId = parent.SupplierId,
            IsActive = true,
            ParentProductId = parentId,
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

        TempData["Success"] = $"Produit unité créé : {enfant.CommercialName} ({nbUnitesParBoite} u./boîte).";
        return RedirectToAction(nameof(Edit), new { id = parentId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.CanModifyPrice)]
    public async Task<IActionResult> Edit(int id,
        [Bind(
            "Id,CommercialName,GenericName,CategoryId,Form,Dosage,SupplierId,PurchasePrice,SalePrice,AlertThreshold,Location,IsActive,TarifType,Cip,ProductType")]
        Product product,
        int? ajustementStock = null,
        string? ajustementRaison = null)
    {
        if (id != product.Id)
            return NotFound();

        var canEditCipAndType = AppRoles.IsTitulaire(User) || User.IsInRole(AppRoles.Pharmacien);
        var canAdjustStock = canEditCipAndType;

        var existing = await _context.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
        if (existing == null)
            return NotFound();

        if (ajustementStock.HasValue && canAdjustStock && ajustementStock.Value != existing.StockQuantity)
        {
            if (ajustementStock.Value < 0)
                ModelState.AddModelError(string.Empty, "La nouvelle quantité ne peut pas être négative.");
            else if (string.IsNullOrWhiteSpace(ajustementRaison))
                ModelState.AddModelError(string.Empty, "La raison de l'ajustement est obligatoire.");
        }

        if (!ModelState.IsValid)
        {
            product.StockQuantity = existing.StockQuantity;
            await PopulateLookupsAsync(product.CategoryId, product.SupplierId);
            return View(product);
        }

        product.StockQuantity = existing.StockQuantity;
        product.Refha = existing.Refha;
        product.ReferencePurchasePrice = existing.ReferencePurchasePrice;
        product.RegulatedSalePrice = existing.RegulatedSalePrice;
        product.ParentProductId = existing.ParentProductId;
        product.NbUnitesParBoite = existing.NbUnitesParBoite;
        product.EstVenteDetail = existing.EstVenteDetail;
        product.Coefficient = existing.Coefficient;
        product.AssujettiTVA = existing.AssujettiTVA;
        product.TauxTVA = existing.TauxTVA;

        if (!canEditCipAndType)
        {
            product.Cip = existing.Cip;
            product.ProductType = existing.ProductType;
        }

        TVACalculator.AppliquerTarif(product);

        if (product.SalePrice != existing.SalePrice)
        {
            _context.PrixModifications.Add(new PrixModification
            {
                ProductId = product.Id,
                AncienPrix = existing.SalePrice,
                NouveauPrix = product.SalePrice,
                ModifiedAt = DateTime.Now,
                ModifiedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
                ModifiedByDisplayName = User.Identity?.Name
                    ?? User.FindFirstValue(ClaimTypes.Name)
                    ?? "",
                Raison = "Modification produit"
            });
        }

        try
        {
            _context.Update(product);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await ProductExistsAsync(product.Id))
                return NotFound();
            throw;
        }

        var successMessage = "Produit mis à jour.";

        if (ajustementStock.HasValue
            && canAdjustStock
            && ajustementStock.Value >= 0
            && ajustementStock.Value != existing.StockQuantity
            && !string.IsNullOrWhiteSpace(ajustementRaison))
        {
            var delta = ajustementStock.Value - existing.StockQuantity;
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var displayName = User.Identity?.Name
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? userId;
            var reason = $"Ajustement manuel — {ajustementRaison.Trim()} (par {displayName})";

            var tracked = await _context.Products.FirstOrDefaultAsync(p => p.Id == product.Id);
            if (tracked == null)
                return NotFound();

            if (delta > 0)
            {
                var lot = await _context.ProductBatches
                    .Where(b => b.ProductId == product.Id)
                    .OrderByDescending(b => b.Id)
                    .FirstOrDefaultAsync();

                if (lot == null)
                {
                    lot = new ProductBatch
                    {
                        ProductId = product.Id,
                        LotNumber = $"AJUST-{product.Id}-{DateTime.Now:yyyyMMddHHmmss}",
                        ExpirationDate = DateTime.Today.AddYears(2),
                        Quantity = delta
                    };
                    _context.ProductBatches.Add(lot);
                    _context.StockMovements.Add(new StockMovement
                    {
                        ProductId = product.Id,
                        Batch = lot,
                        Type = StockMovementType.Entree,
                        Quantity = delta,
                        Reason = reason,
                        OccurredAt = DateTime.Now,
                        UserId = userId
                    });
                }
                else
                {
                    lot.Quantity += delta;
                    _context.StockMovements.Add(new StockMovement
                    {
                        ProductId = product.Id,
                        BatchId = lot.Id,
                        Type = StockMovementType.Entree,
                        Quantity = delta,
                        Reason = reason,
                        OccurredAt = DateTime.Now,
                        UserId = userId
                    });
                }

                tracked.StockQuantity = ajustementStock.Value;
                await _context.SaveChangesAsync();
                successMessage =
                    $"Produit mis à jour. Stock ajusté de {existing.StockQuantity} à {ajustementStock.Value} unités. Mouvement tracé.";
            }
            else
            {
                // Diminution : FIFO sur les lots, jamais de quantité négative
                var toRemove = Math.Abs(delta);
                var lots = await _context.ProductBatches
                    .Where(b => b.ProductId == product.Id && b.Quantity > 0)
                    .OrderBy(b => b.ExpirationDate)
                    .ThenBy(b => b.Id)
                    .ToListAsync();

                var removed = 0;
                foreach (var lot in lots)
                {
                    if (toRemove <= 0)
                        break;

                    var take = Math.Min(lot.Quantity, toRemove);
                    if (take <= 0)
                        continue;

                    lot.Quantity -= take;
                    toRemove -= take;
                    removed += take;

                    _context.StockMovements.Add(new StockMovement
                    {
                        ProductId = product.Id,
                        BatchId = lot.Id,
                        Type = StockMovementType.Sortie,
                        Quantity = take,
                        Reason = reason,
                        OccurredAt = DateTime.Now,
                        UserId = userId
                    });
                }

                tracked.StockQuantity = ajustementStock.Value;
                await _context.SaveChangesAsync();

                if (toRemove > 0)
                {
                    TempData["Warning"] =
                        $"Stock produit fixé à {ajustementStock.Value} unités, mais seulement {removed} unité(s) " +
                        $"ont pu être retirées des lots (lots insuffisants — écart inventaire/lots).";
                    successMessage =
                        $"Produit mis à jour. Stock ajusté de {existing.StockQuantity} à {ajustementStock.Value} unités.";
                }
                else if (removed == 0)
                {
                    TempData["Warning"] =
                        $"Stock produit fixé à {ajustementStock.Value} unités, mais aucun lot positif n'existait. " +
                        "Créez ou corrigez les lots si nécessaire.";
                    successMessage =
                        $"Produit mis à jour. Stock ajusté de {existing.StockQuantity} à {ajustementStock.Value} unités.";
                }
                else
                {
                    successMessage =
                        $"Produit mis à jour. Stock ajusté de {existing.StockQuantity} à {ajustementStock.Value} unités. Mouvement tracé.";
                }
            }
        }

        TempData["Success"] = successMessage;
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = AppRoles.CanModifyPrice)]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
            return NotFound();

        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(m => m.Id == id);
        if (product == null)
            return NotFound();

        return View(product);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.CanModifyPrice)]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
            return RedirectToAction(nameof(Index));

        _context.Products.Remove(product);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 547 })
        {
            TempData["Error"] =
                "Impossible de supprimer ce produit car il possède un historique de stock ou de ventes.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Produit supprimé.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.PharmacienTitulaire},{AppRoles.Administrateur}")]
    public async Task<IActionResult> PrixModifications()
    {
        var list = await _context.PrixModifications
            .AsNoTracking()
            .Include(p => p.Product)
            .OrderByDescending(p => p.ModifiedAt)
            .Take(200)
            .ToListAsync();

        return View(list);
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.CanManageUsers},{AppRoles.Administrateur}")]
    public async Task<IActionResult> Classify(string? term = null, int? filterType = null, int page = 1)
    {
        if (page < 1)
            page = 1;

        var q = _context.Products.AsNoTracking().AsQueryable();

        if (filterType.HasValue && Enum.IsDefined(typeof(ProductType), filterType.Value))
            q = q.Where(p => (int)p.ProductType == filterType.Value);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var pattern = term.Trim();
            q = q.Where(p =>
                p.CommercialName.Contains(pattern)
                || (p.Cip != null && p.Cip.Contains(pattern)));
        }

        var unknownCount = await _context.Products
            .AsNoTracking()
            .CountAsync(p => p.ProductType == ProductType.Inconnu);

        var totalCount = await q.CountAsync();
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)ClassifyPageSize);
        if (page > totalPages)
            page = totalPages;

        var products = await q
            .OrderBy(p => p.CommercialName)
            .Skip((page - 1) * ClassifyPageSize)
            .Take(ClassifyPageSize)
            .Select(p => new ProductClassificationRowViewModel
            {
                Id = p.Id,
                Cip = p.Cip,
                CommercialName = p.CommercialName,
                ProductType = p.ProductType,
                SupplierName = p.Supplier != null ? p.Supplier.Name : null
            })
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;

        var paginationRoutes = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(term))
            paginationRoutes["term"] = term.Trim();
        if (filterType.HasValue && Enum.IsDefined(typeof(ProductType), filterType.Value))
            paginationRoutes["filterType"] = filterType.Value.ToString();
        ViewBag.PaginationRoutes = paginationRoutes;
        ViewBag.PaginationAction = "Classify";

        var model = new ProductClassificationIndexViewModel
        {
            Products = products,
            Term = term,
            FilterType = filterType,
            CurrentPage = page,
            TotalPages = totalPages,
            TotalCount = totalCount,
            UnknownCount = unknownCount,
            ProductTypes = BuildProductTypeFilterItems(filterType)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.CanManageUsers},{AppRoles.Administrateur}")]
    public async Task<IActionResult> ClassifyBulk(
        List<int> productIds,
        int newType,
        string? returnTerm = null,
        int? returnFilterType = null,
        int returnPage = 1)
    {
        if (!Enum.IsDefined(typeof(ProductType), newType))
        {
            TempData["Error"] = "Type de produit invalide.";
            return RedirectToAction(nameof(Classify), new
            {
                term = returnTerm,
                filterType = returnFilterType,
                page = returnPage
            });
        }

        if (productIds == null || productIds.Count == 0)
        {
            TempData["Warning"] = "Aucun produit sélectionné.";
            return RedirectToAction(nameof(Classify), new
            {
                term = returnTerm,
                filterType = returnFilterType,
                page = returnPage
            });
        }

        var selectedType = (ProductType)newType;
        var products = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        foreach (var product in products)
            product.ProductType = selectedType;

        await _context.SaveChangesAsync();

        TempData["Success"] = $"{products.Count} produit(s) classifié(s) en {ProductTypeDisplayLabel(selectedType)}.";

        return RedirectToAction(nameof(Classify), new
        {
            term = returnTerm,
            filterType = returnFilterType,
            page = returnPage
        });
    }

    [HttpGet]
    [Authorize(Roles = AppRoles.CatalogManage)]
    public async Task<IActionResult> Anomalies(string? filtre = null, int page = 1)
    {
        const string categorieACategoriser = "À catégoriser";
        const int pageSize = 20;
        if (page < 1)
            page = 1;

        // Parents + tablettes avec anomalies (les tablettes restent visibles pour les prix)
        var query = _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Include(p => p.ParentProduct)
            .Where(p => p.IsActive &&
                (p.SalePrice == 0
                 || p.PurchasePrice == 0
                 || p.ProductType == ProductType.Inconnu
                 || p.AlertThreshold == 0
                 || (p.Category != null && p.Category.Name == categorieACategoriser)));

        var allForCounts = await query.ToListAsync();
        ViewBag.PrixVenteZero = allForCounts.Count(p => p.SalePrice == 0);
        ViewBag.PrixAchatZero = allForCounts.Count(p => p.PurchasePrice == 0);
        ViewBag.TypeInconnu = allForCounts.Count(p => p.ProductType == ProductType.Inconnu);
        ViewBag.SeuilZero = allForCounts.Count(p => p.AlertThreshold == 0);
        ViewBag.SansCategorie = allForCounts.Count(p =>
            p.Category != null && p.Category.Name == categorieACategoriser);
        ViewBag.TotalAnomalies = allForCounts.Count;
        ViewBag.Filtre = filtre;

        if (filtre == "prix" || filtre == "prixvente")
            query = query.Where(p => p.SalePrice == 0);
        else if (filtre == "prixachat")
            query = query.Where(p => p.PurchasePrice == 0);
        else if (filtre == "type")
            query = query.Where(p => p.ProductType == ProductType.Inconnu);
        else if (filtre == "seuil")
            query = query.Where(p => p.AlertThreshold == 0);
        else if (filtre == "categorie")
            query = query.Where(p => p.Category != null && p.Category.Name == categorieACategoriser);

        var total = await query.CountAsync();
        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (decimal)pageSize);
        if (page > totalPages)
            page = totalPages;

        var produits = await query
            .OrderBy(p => p.CommercialName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Page = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.Total = total;
        ViewBag.Categories = await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        return View(produits);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.PharmacienTitulaire},{AppRoles.Pharmacien}")]
    public async Task<IActionResult> CorrigerLigneAnomalie(
        int productId,
        decimal? purchasePrice,
        decimal? salePrice,
        string? productType,
        int? categoryId,
        int? alertThreshold,
        string? filtre = null,
        int page = 1)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == productId);
        if (product == null)
        {
            if (IsAjaxRequest())
                return Json(new { ok = false, message = "Produit introuvable." });
            TempData["Error"] = "Produit introuvable.";
            return RedirectToAction(nameof(Anomalies), new { filtre, page });
        }

        var changed = false;

        if (purchasePrice.HasValue && purchasePrice.Value >= 0 && purchasePrice.Value != product.PurchasePrice)
        {
            product.PurchasePrice = purchasePrice.Value;
            changed = true;
        }

        if (salePrice.HasValue && salePrice.Value >= 0 && salePrice.Value != product.SalePrice)
        {
            var ancien = product.SalePrice;
            product.SalePrice = salePrice.Value;
            _context.PrixModifications.Add(new PrixModification
            {
                ProductId = product.Id,
                AncienPrix = ancien,
                NouveauPrix = salePrice.Value,
                ModifiedAt = DateTime.Now,
                ModifiedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
                ModifiedByDisplayName = User.Identity?.Name
                    ?? User.FindFirstValue(ClaimTypes.Name)
                    ?? "",
                Raison = "Correction anomalie (ligne)"
            });
            changed = true;
        }

        if (!string.IsNullOrWhiteSpace(productType)
            && Enum.TryParse<ProductType>(productType, out var pt)
            && pt != product.ProductType)
        {
            product.ProductType = pt;
            changed = true;
        }

        if (categoryId.HasValue && categoryId.Value > 0 && categoryId.Value != product.CategoryId)
        {
            var catExists = await _context.Categories.AnyAsync(c => c.Id == categoryId.Value);
            if (catExists)
            {
                product.CategoryId = categoryId.Value;
                changed = true;
            }
        }

        if (alertThreshold.HasValue && alertThreshold.Value >= 0 && alertThreshold.Value != product.AlertThreshold)
        {
            product.AlertThreshold = alertThreshold.Value;
            changed = true;
        }

        if (!changed)
        {
            if (IsAjaxRequest())
                return Json(new { ok = false, message = "Aucune modification à enregistrer." });
            TempData["Error"] = "Aucune modification à enregistrer.";
            return RedirectToAction(nameof(Anomalies), new { filtre, page });
        }

        await _context.SaveChangesAsync();

        await _context.Entry(product).Reference(p => p.Category).LoadAsync();
        var stillVisible = ProductMatchesAnomalyFiltre(product, filtre);
        var counts = await GetAnomalyCountsAsync();
        var message = $"« {product.CommercialName} » corrigé.";

        if (IsAjaxRequest())
        {
            return Json(new
            {
                ok = true,
                message,
                productId,
                removed = !stillVisible,
                counts
            });
        }

        TempData["Success"] = message;
        return RedirectToAction(nameof(Anomalies), new { filtre, page });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.PharmacienTitulaire},{AppRoles.Pharmacien}")]
    public async Task<IActionResult> CorrigerGroupeAnomalie(
        List<int> productIds,
        string champAcorriger,
        string nouvelleValeur,
        string? filtre = null)
    {
        if (productIds == null || productIds.Count == 0)
        {
            TempData["Error"] = "Sélectionnez au moins un produit.";
            return RedirectToAction(nameof(Anomalies), new { filtre });
        }

        var produits = await _context.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        foreach (var p in produits)
        {
            switch (champAcorriger)
            {
                case "ProductType":
                    if (Enum.TryParse<ProductType>(nouvelleValeur, out var pt))
                        p.ProductType = pt;
                    break;
                case "TarifType":
                {
                    TarifType? tt = null;
                    if (Enum.TryParse<TarifType>(nouvelleValeur, ignoreCase: true, out var parsedName))
                        tt = parsedName;
                    else if (int.TryParse(nouvelleValeur, out var ttInt)
                             && Enum.IsDefined(typeof(TarifType), ttInt))
                        tt = (TarifType)ttInt;

                    if (tt.HasValue)
                    {
                        p.TarifType = tt.Value;
                        TVACalculator.AppliquerTarif(p);
                    }
                    break;
                }
                case "MinStockLevel":
                case "AlertThreshold":
                    if (int.TryParse(nouvelleValeur, out var sl))
                        p.AlertThreshold = sl;
                    break;
                case "CategoryId":
                    if (int.TryParse(nouvelleValeur, out var catId) && catId > 0)
                    {
                        var exists = await _context.Categories.AnyAsync(c => c.Id == catId);
                        if (exists)
                            p.CategoryId = catId;
                    }
                    break;
            }
        }

        await _context.SaveChangesAsync();

        TempData["Success"] = $"{produits.Count} produit(s) corrigé(s) avec succès.";
        return RedirectToAction(nameof(Anomalies), new { filtre });
    }

    private async Task<bool> ProductExistsAsync(int id) =>
        await _context.Products.AnyAsync(e => e.Id == id);

    private bool IsAjaxRequest() =>
        string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    private const string CategorieACategoriser = "À catégoriser";

    private static bool ProductHasAnomaly(Product p) =>
        p.SalePrice == 0
        || p.PurchasePrice == 0
        || p.ProductType == ProductType.Inconnu
        || p.AlertThreshold == 0
        || (p.Category != null && p.Category.Name == CategorieACategoriser);

    /// <summary>True si le produit doit encore apparaître dans la liste Anomalies pour ce filtre.</summary>
    private static bool ProductMatchesAnomalyFiltre(Product p, string? filtre)
    {
        if (!ProductHasAnomaly(p))
            return false;

        return filtre switch
        {
            "prix" or "prixvente" => p.SalePrice == 0,
            "prixachat" => p.PurchasePrice == 0,
            "type" => p.ProductType == ProductType.Inconnu,
            "seuil" => p.AlertThreshold == 0,
            "categorie" => p.Category != null && p.Category.Name == CategorieACategoriser,
            _ => true
        };
    }

    private async Task<object> GetAnomalyCountsAsync()
    {
        var list = await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive &&
                (p.SalePrice == 0
                 || p.PurchasePrice == 0
                 || p.ProductType == ProductType.Inconnu
                 || p.AlertThreshold == 0
                 || (p.Category != null && p.Category.Name == CategorieACategoriser)))
            .ToListAsync();

        return new
        {
            prixVenteZero = list.Count(p => p.SalePrice == 0),
            prixAchatZero = list.Count(p => p.PurchasePrice == 0),
            typeInconnu = list.Count(p => p.ProductType == ProductType.Inconnu),
            seuilZero = list.Count(p => p.AlertThreshold == 0),
            sansCategorie = list.Count(p => p.Category != null && p.Category.Name == CategorieACategoriser),
            total = list.Count
        };
    }

    private async Task PopulateLookupsAsync(int? selectedCategoryId = null, int? selectedSupplierId = null)
    {
        var categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
        var suppliers = await _context.Suppliers.OrderBy(s => s.Name).ToListAsync();

        ViewData["CategoryId"] = new SelectList(categories, "Id", "Name", selectedCategoryId);
        ViewData["SupplierId"] = new SelectList(suppliers, "Id", "Name", selectedSupplierId);
    }

    private IQueryable<Product> FilteredProductsQuery(ProductListFilters filter)
    {
        var q = _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Include(p => p.ChildProducts)
            .Where(p => p.ParentProductId == null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var term = filter.Q.Trim();
            q = q.Where(p =>
                p.CommercialName.Contains(term)
                || (p.GenericName != null && p.GenericName.Contains(term))
                || p.ChildProducts.Any(c =>
                    c.CommercialName.Contains(term)
                    || (c.GenericName != null && c.GenericName.Contains(term))));
        }

        if (filter.CategoryId > 0)
            q = q.Where(p => p.CategoryId == filter.CategoryId);

        if (filter.SupplierId > 0)
            q = q.Where(p => p.SupplierId == filter.SupplierId);

        if (filter.Stock == "low")
            q = q.Where(p => p.IsActive && p.StockQuantity > 0 && p.StockQuantity <= p.AlertThreshold);
        else if (filter.Stock == "out")
            q = q.Where(p => p.IsActive && p.StockQuantity == 0);

        if (filter.Active == "1")
            q = q.Where(p => p.IsActive);
        else if (filter.Active == "0")
            q = q.Where(p => !p.IsActive);

        return q;
    }

    private static List<SelectListItem> BuildProductTypeFilterItems(int? selectedType)
    {
        var items = new List<SelectListItem>
        {
            new("Tous les types", "", selectedType == null)
        };

        foreach (ProductType type in Enum.GetValues<ProductType>())
        {
            items.Add(new SelectListItem(ProductTypeDisplayLabel(type), ((int)type).ToString(), selectedType == (int)type));
        }

        return items;
    }

    private static string ProductTypeDisplayLabel(ProductType type) => type switch
    {
        ProductType.Medicament => "Médicament",
        ProductType.Parapharmacie => "Parapharmacie",
        _ => "Inconnu"
    };
}
