using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Data;
using Pharmacie.Models;

namespace Pharmacie.Controllers;

[Authorize]
public class AlertsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AlertsController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index(
        int? horizonDays = null,
        int pageStock = 1,
        int pageExpiration = 1,
        int page = 1,
        string? section = null)
    {
        const int pageSize = 50;

        if (section == "stock")
            pageStock = page;
        else if (section == "expiration")
            pageExpiration = page;

        if (pageStock < 1)
            pageStock = 1;
        if (pageExpiration < 1)
            pageExpiration = 1;

        var defaultHorizon = _configuration.GetValue<int>("Alerts:ExpirationHorizonDays", 90);
        var effectiveHorizon = Math.Clamp(horizonDays ?? defaultHorizon, 7, 365);

        var today = DateTime.Today;
        var horizon = today.AddDays(effectiveHorizon);

        var lowStockQuery = _context.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.StockQuantity <= p.AlertThreshold);

        var stockTotalCount = await lowStockQuery.CountAsync();
        var stockTotalPages = stockTotalCount == 0 ? 1 : (int)Math.Ceiling(stockTotalCount / (double)pageSize);
        if (pageStock > stockTotalPages)
            pageStock = stockTotalPages;

        var lowStock = await lowStockQuery
            .OrderBy(p => p.StockQuantity)
            .ThenBy(p => p.CommercialName)
            .Skip((pageStock - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var expirationQuery = _context.ProductBatches
            .AsNoTracking()
            .Include(b => b.Product)
            .Where(b => b.Quantity > 0 && b.ExpirationDate <= horizon);

        var expirationTotalCount = await expirationQuery.CountAsync();
        var expirationTotalPages = expirationTotalCount == 0 ? 1 : (int)Math.Ceiling(expirationTotalCount / (double)pageSize);
        if (pageExpiration > expirationTotalPages)
            pageExpiration = expirationTotalPages;

        var batches = await expirationQuery
            .OrderBy(b => b.ExpirationDate)
            .ThenBy(b => b.Product!.CommercialName)
            .ThenBy(b => b.LotNumber)
            .Skip((pageExpiration - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.HorizonDays = effectiveHorizon;
        ViewBag.DefaultHorizonDays = defaultHorizon;

        ViewBag.StockCurrentPage = pageStock;
        ViewBag.StockTotalPages = stockTotalPages;
        ViewBag.StockTotalCount = stockTotalCount;

        ViewBag.ExpirationCurrentPage = pageExpiration;
        ViewBag.ExpirationTotalPages = expirationTotalPages;
        ViewBag.ExpirationTotalCount = expirationTotalCount;

        var paginationRoutesStock = new Dictionary<string, string>
        {
            ["horizonDays"] = effectiveHorizon.ToString(),
            ["pageExpiration"] = pageExpiration.ToString(),
            ["section"] = "stock"
        };
        ViewBag.PaginationRoutesStock = paginationRoutesStock;

        var paginationRoutesExpiration = new Dictionary<string, string>
        {
            ["horizonDays"] = effectiveHorizon.ToString(),
            ["pageStock"] = pageStock.ToString(),
            ["section"] = "expiration"
        };
        ViewBag.PaginationRoutesExpiration = paginationRoutesExpiration;

        var vm = new AlertsIndexViewModel
        {
            LowStockProducts = lowStock,
            BatchesNearingExpiration = batches,
            Today = today,
            HorizonDays = effectiveHorizon
        };

        return View(vm);
    }
}
