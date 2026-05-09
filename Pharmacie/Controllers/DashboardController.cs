using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Services;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.DashboardAccess)]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;

    public DashboardController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        var horizonEnd = today.AddDays(AlertsIndexViewModel.ExpirationHorizonDays);

        var vm = new DashboardViewModel
        {
            Today = today,
            TotalProducts = await _db.Products.CountAsync(),
            LowStockProductsCount = await _db.Products.CountAsync(p =>
                p.IsActive && p.StockQuantity <= p.AlertThreshold),
            OutOfStockProductsCount = await _db.Products.CountAsync(p =>
                p.IsActive && p.StockQuantity == 0),
            NearExpiryLotsCount = await _db.ProductBatches.CountAsync(b =>
                b.Quantity > 0
                && b.ExpirationDate.Date >= today
                && b.ExpirationDate.Date <= horizonEnd),
            ExpiredLotsCount = await _db.ProductBatches.CountAsync(b =>
                b.Quantity > 0 && b.ExpirationDate.Date < today),
            PendingPurchaseOrdersCount = await _db.PurchaseOrders.CountAsync(o =>
                o.Status == PurchaseOrderStatus.Envoyee
                || o.Status == PurchaseOrderStatus.PartiellementRecue),
            SalesTodayCount = await _db.Sales.CountAsync(s => s.SoldAt.Date == today),
            SalesTodayTotal = await _db.Sales
                .Where(s => s.SoldAt.Date == today)
                .SelectMany(s => s.Lines)
                .SumAsync(l => l.Quantity * l.UnitPrice)
        };

        var movements = await _db.StockMovements
            .AsNoTracking()
            .Include(m => m.Product)
            .Include(m => m.Batch)
            .OrderByDescending(m => m.OccurredAt)
            .ThenByDescending(m => m.Id)
            .Take(10)
            .ToListAsync();

        var movementUserLabels = await UserDisplayResolver.LoadLabelsByIdAsync(_db, movements.Select(m => m.UserId));
        vm.RecentMovements = movements.Select(m => new DashboardMovementRow
        {
            Id = m.Id,
            OccurredAt = m.OccurredAt,
            ProductName = m.Product?.CommercialName ?? $"#{m.ProductId}",
            LotLabel = m.Batch?.LotNumber ?? "—",
            Type = m.Type,
            Quantity = m.Quantity,
            Reason = m.Reason,
            ResponsibleDisplay = UserDisplayResolver.Resolve(movementUserLabels, m.UserId),
            SaleId = m.SaleId
        }).ToList();

        var recentSales = await _db.Sales
            .AsNoTracking()
            .Include(s => s.Lines)
            .OrderByDescending(s => s.SoldAt)
            .ThenByDescending(s => s.Id)
            .Take(10)
            .ToListAsync();

        var saleUserLabels = await UserDisplayResolver.LoadLabelsByIdAsync(_db, recentSales.Select(s => s.UserId));
        vm.RecentSales = recentSales.Select(s => new DashboardSaleRow
        {
            Id = s.Id,
            SoldAt = s.SoldAt,
            LineCount = s.Lines.Count,
            Total = s.Lines.Sum(l => l.Quantity * l.UnitPrice),
            RecordedByDisplay = UserDisplayResolver.Resolve(saleUserLabels, s.UserId)
        }).ToList();

        vm.ShowPatientDashboardWidget = AppRoles.CanAccessPatientsRead(User) && AppRoles.CanAccessDashboard(User);
        if (vm.ShowPatientDashboardWidget)
        {
            vm.PatientRemindersDueCount = await _db.PatientTreatmentReminders.CountAsync(r =>
                !r.IsDone && r.ReminderDate <= today);
        }

        return View(vm);
    }
}
