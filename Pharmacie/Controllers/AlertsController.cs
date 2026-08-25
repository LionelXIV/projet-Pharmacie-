using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.AlertsAccess)]
public class AlertsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AlertsController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index(int? horizon = null, int? horizonDays = null, int? categorieId = null)
    {
        var vm = await LoadAlertesAsync(horizon, horizonDays, categorieId);
        PopulateAlertesViewBag(vm);
        return View(vm);
    }

    public async Task<IActionResult> ExportAlertesPdf(int? horizon = null, int? horizonDays = null, int? categorieId = null)
    {
        var vm = await LoadAlertesAsync(horizon, horizonDays, categorieId);
        PopulateAlertesViewBag(vm);
        return View(vm);
    }

    private async Task<AlertsIndexViewModel> LoadAlertesAsync(int? horizon, int? horizonDays, int? categorieId)
    {
        var defaultHorizon = _configuration.GetValue<int>("Alerts:ExpirationHorizonDays", 90);
        var effectiveHorizon = Math.Clamp(horizon ?? horizonDays ?? defaultHorizon, 7, 365);
        var today = DateTime.Today;
        var dateHorizon = today.AddDays(effectiveHorizon);

        var productsQuery = _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Where(p => p.IsActive && (p.Category == null || !p.Category.EstHorsSysteme));

        if (categorieId is > 0)
            productsQuery = productsQuery.Where(p => p.CategoryId == categorieId.Value);

        var ruptures = await productsQuery
            .Where(p => p.StockQuantity == 0)
            .OrderBy(p => p.CommercialName)
            .ToListAsync();

        var stockFaible = await productsQuery
            .Where(p => p.StockQuantity > 0
                        && p.AlertThreshold > 0
                        && p.StockQuantity <= p.AlertThreshold)
            .OrderBy(p => p.StockQuantity)
            .ThenBy(p => p.CommercialName)
            .ToListAsync();

        var batchesQuery = _context.ProductBatches
            .AsNoTracking()
            .Include(b => b.Product!)
                .ThenInclude(p => p.Category)
            .Where(b =>
                b.Quantity > 0
                && b.Product != null
                && b.Product.IsActive
                && (b.Product.Category == null || !b.Product.Category.EstHorsSysteme));

        if (categorieId is > 0)
            batchesQuery = batchesQuery.Where(b => b.Product!.CategoryId == categorieId.Value);

        var lotsExpires = await batchesQuery
            .Where(b => b.ExpirationDate < today)
            .OrderBy(b => b.ExpirationDate)
            .ThenBy(b => b.Product!.CommercialName)
            .ToListAsync();

        var peremptionsProches = await batchesQuery
            .Where(b => b.ExpirationDate >= today && b.ExpirationDate <= dateHorizon)
            .OrderBy(b => b.ExpirationDate)
            .ThenBy(b => b.Product!.CommercialName)
            .ToListAsync();

        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => !c.EstHorsSysteme)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return new AlertsIndexViewModel
        {
            HorizonDays = effectiveHorizon,
            CategorieId = categorieId is > 0 ? categorieId : null,
            Ruptures = ruptures,
            StockFaible = stockFaible,
            LotsExpires = lotsExpires,
            PeremptionsProches = peremptionsProches,
            Categories = categories,
            Today = today
        };
    }

    private void PopulateAlertesViewBag(AlertsIndexViewModel vm)
    {
        ViewBag.Ruptures = vm.Ruptures;
        ViewBag.StockFaible = vm.StockFaible;
        ViewBag.LotsExpires = vm.LotsExpires;
        ViewBag.PeremptionsProches = vm.PeremptionsProches;
        ViewBag.NbRuptures = vm.Ruptures.Count;
        ViewBag.NbFaible = vm.StockFaible.Count;
        ViewBag.NbExpires = vm.LotsExpires.Count;
        ViewBag.NbProches = vm.PeremptionsProches.Count;
        ViewBag.Categories = vm.Categories;
        ViewBag.Horizon = vm.HorizonDays;
        ViewBag.CategorieId = vm.CategorieId;
    }
}
