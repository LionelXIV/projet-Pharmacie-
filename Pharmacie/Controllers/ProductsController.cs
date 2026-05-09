using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Reporting;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.Catalog)]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ProductsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index([FromQuery] ProductListFilters? filter)
    {
        filter ??= new ProductListFilters();
        var list = await FilteredProductsQuery(filter)
            .OrderBy(p => p.CommercialName)
            .ToListAsync();

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

    public async Task<IActionResult> Create()
    {
        await PopulateLookupsAsync();
        return View(new Product());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(
            "CommercialName,GenericName,CategoryId,Form,Dosage,SupplierId,PurchasePrice,SalePrice,StockQuantity,AlertThreshold,Location,IsActive")]
        Product product)
    {
        if (ModelState.IsValid)
        {
            _context.Add(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        await PopulateLookupsAsync(product.CategoryId, product.SupplierId);
        return View(product);
    }

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
    public async Task<IActionResult> Edit(int id,
        [Bind(
            "Id,CommercialName,GenericName,CategoryId,Form,Dosage,SupplierId,PurchasePrice,SalePrice,StockQuantity,AlertThreshold,Location,IsActive")]
        Product product)
    {
        if (id != product.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
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
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product != null)
        {
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
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
}
