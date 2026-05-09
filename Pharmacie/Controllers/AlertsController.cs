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

    public AlertsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        var horizon = today.AddDays(AlertsIndexViewModel.ExpirationHorizonDays);

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

        var vm = new AlertsIndexViewModel
        {
            LowStockProducts = lowStock,
            BatchesNearingExpiration = batches,
            Today = today
        };

        return View(vm);
    }
}
