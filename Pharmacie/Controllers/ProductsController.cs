using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Reporting;

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

    [Authorize(Roles = AppRoles.Catalog)]
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
                    : p.CommercialName) + " (stock: " + p.StockQuantity + ")",
                salePrice = p.SalePrice,
                purchasePrice = p.PurchasePrice,
                stockQuantity = p.StockQuantity
            })
            .ToListAsync();

        return Json(results);
    }

    [Authorize(Roles = AppRoles.Catalog)]
    public async Task<IActionResult> IndexCsv([FromQuery] ProductListFilters? filter)
    {
        filter ??= new ProductListFilters();
        var list = await FilteredProductsQuery(filter)
            .OrderBy(p => p.CommercialName)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',',
            ReportCsvFormatter.Escape("N°"),
            ReportCsvFormatter.Escape("Nom commercial"),
            ReportCsvFormatter.Escape("Nom générique"),
            ReportCsvFormatter.Escape("Catégorie"),
            ReportCsvFormatter.Escape("Forme"),
            ReportCsvFormatter.Escape("Dosage"),
            ReportCsvFormatter.Escape("Fournisseur"),
            ReportCsvFormatter.Escape("Prix achat (EUR)"),
            ReportCsvFormatter.Escape("Prix vente (EUR)"),
            ReportCsvFormatter.Escape("Stock"),
            ReportCsvFormatter.Escape("Seuil"),
            ReportCsvFormatter.Escape("Emplacement"),
            ReportCsvFormatter.Escape("Actif")));

        foreach (var p in list)
        {
            sb.AppendLine(string.Join(',',
                ReportCsvFormatter.IntInvariant(p.Id),
                ReportCsvFormatter.Escape(p.CommercialName),
                ReportCsvFormatter.Escape(p.GenericName ?? ""),
                ReportCsvFormatter.Escape(p.Category?.Name ?? ""),
                ReportCsvFormatter.Escape(p.Form ?? ""),
                ReportCsvFormatter.Escape(p.Dosage ?? ""),
                ReportCsvFormatter.Escape(p.Supplier?.Name ?? ""),
                ReportCsvFormatter.DecimalInvariant(p.PurchasePrice),
                ReportCsvFormatter.DecimalInvariant(p.SalePrice),
                ReportCsvFormatter.IntInvariant(p.StockQuantity),
                ReportCsvFormatter.IntInvariant(p.AlertThreshold),
                ReportCsvFormatter.Escape(p.Location ?? ""),
                p.IsActive ? ReportCsvFormatter.Escape("Oui") : ReportCsvFormatter.Escape("Non")));
        }

        var bytes = ReportCsvFormatter.ToUtf8BytesWithBom(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", ReportCsvFormatter.FileName("export-catalogue-produits"));
    }

    [Authorize(Roles = AppRoles.Catalog)]
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

    [Authorize(Roles = AppRoles.Catalog)]
    public async Task<IActionResult> Create()
    {
        await PopulateLookupsAsync();
        return View(new Product());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Catalog)]
    public async Task<IActionResult> Create(
        [Bind(
            "CommercialName,GenericName,CategoryId,Form,Dosage,SupplierId,PurchasePrice,SalePrice,AlertThreshold,Location,IsActive")]
        Product product)
    {
        if (ModelState.IsValid)
        {
            product.StockQuantity = 0;
            _context.Add(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        await PopulateLookupsAsync(product.CategoryId, product.SupplierId);
        return View(product);
    }

    [Authorize(Roles = AppRoles.Catalog)]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
            return NotFound();

        var product = await _context.Products.FindAsync(id);
        if (product == null)
            return NotFound();

        await PopulateLookupsAsync(product.CategoryId, product.SupplierId);
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AppRoles.Catalog)]
    public async Task<IActionResult> Edit(int id,
        [Bind(
            "Id,CommercialName,GenericName,CategoryId,Form,Dosage,SupplierId,PurchasePrice,SalePrice,AlertThreshold,Location,IsActive")]
        Product product)
    {
        if (id != product.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            var existing = await _context.Products.AsNoTracking()
                .Select(p => new { p.Id, p.StockQuantity })
                .FirstOrDefaultAsync(p => p.Id == id);
            if (existing == null)
                return NotFound();

            product.StockQuantity = existing.StockQuantity;

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

            return RedirectToAction(nameof(Index));
        }

        await PopulateLookupsAsync(product.CategoryId, product.SupplierId);
        return View(product);
    }

    [Authorize(Roles = AppRoles.Catalog)]
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
    [Authorize(Roles = AppRoles.Catalog)]
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

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Administrateur},{AppRoles.Pharmacien}")]
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
    [Authorize(Roles = $"{AppRoles.Administrateur},{AppRoles.Pharmacien}")]
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

    private async Task<bool> ProductExistsAsync(int id) =>
        await _context.Products.AnyAsync(e => e.Id == id);

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
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var term = filter.Q.Trim();
            q = q.Where(p =>
                p.CommercialName.Contains(term)
                || (p.GenericName != null && p.GenericName.Contains(term)));
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
