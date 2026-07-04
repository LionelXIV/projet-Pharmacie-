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

    public async Task<IActionResult> Index(int? horizonDays = null)
    {
        var defaultHorizon = _configuration.GetValue<int>("Alerts:ExpirationHorizonDays", 90);
        var effectiveHorizon = Math.Clamp(horizonDays ?? defaultHorizon, 7, 365);

        var today = DateTime.Today;
        var horizon = today.AddDays(effectiveHorizon);

        var lowStock = await _context.Products
            .Where(p => p.IsActive && p.StockQuantity <= p.AlertThreshold)
            .OrderBy(p => p.StockQuantity)
            .ThenBy(p => p.CommercialName)
            .ToListAsync();

        var batches = await _context.ProductBatches
            .Include(b => b.Product)
            .Where(b => b.Quantity > 0 && b.ExpirationDate <= horizon)
            .OrderBy(b => b.ExpirationDate)
            .ThenBy(b => b.Product!.CommercialName)
            .ThenBy(b => b.LotNumber)
            .ToListAsync();

        ViewBag.HorizonDays = effectiveHorizon;
        ViewBag.DefaultHorizonDays = defaultHorizon;

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
