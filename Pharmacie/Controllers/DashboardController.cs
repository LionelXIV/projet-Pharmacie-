using System.Globalization;
using System.Text.Json;
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
    private readonly IConfiguration _configuration;

    public DashboardController(ApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        var horizon = _configuration.GetValue<int>("Alerts:ExpirationHorizonDays", 90);
        var horizonEnd = today.AddDays(horizon);

        var todaySales = await _db.Sales
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Product)
            .Where(s => s.SoldAt.Date == today)
            .ToListAsync();

        var caDuJour = todaySales
            .SelectMany(s => s.Lines)
            .Sum(l => l.UnitPrice * l.Quantity);

        var vm = new DashboardViewModel
        {
            Today = today,
            ExpirationHorizonDays = horizon,
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
            SalesTodayCount = todaySales.Count,
            SalesTodayTotal = caDuJour
        };

        if (AppRoles.CanAccessFinances(User))
        {
            var margeBrute = todaySales
                .SelectMany(s => s.Lines)
                .Sum(l => (l.UnitPrice - (l.Product?.PurchasePrice ?? 0m)) * l.Quantity);
            var nbVentes = todaySales.Count;
            var panierMoyen = nbVentes > 0 ? caDuJour / nbVentes : 0m;

            ViewBag.ShowFinances = true;
            ViewBag.MargeBrute = margeBrute;
            ViewBag.PanierMoyen = panierMoyen;
            ViewBag.NbVentes = nbVentes;
            ViewBag.CaDuJour = caDuJour;
        }

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

        var chartStart = today.AddDays(-29);
        var salesInRange = await _db.Sales
            .AsNoTracking()
            .Where(s => s.SoldAt.Date >= chartStart && s.SoldAt.Date <= today)
            .Include(s => s.Lines)
            .ToListAsync();

        var salesByDay = salesInRange
            .GroupBy(s => s.SoldAt.Date)
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(s => s.Lines).Sum(l => l.Quantity * l.UnitPrice));

        var salesChartData = Enumerable.Range(0, 30)
            .Select(offset =>
            {
                var day = chartStart.AddDays(offset);
                salesByDay.TryGetValue(day, out var amount);
                return new
                {
                    date = day.ToString("dd/MM", CultureInfo.InvariantCulture),
                    amount
                };
            })
            .ToList();

        ViewBag.SalesChartData = JsonSerializer.Serialize(salesChartData);

        var stockChartData = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .GroupBy(p => p.Category != null ? p.Category!.Name : "Sans catégorie")
            .Select(g => new
            {
                category = g.Key,
                value = g.Sum(p => p.StockQuantity * p.SalePrice)
            })
            .OrderByDescending(x => x.value)
            .Take(8)
            .ToListAsync();

        ViewBag.StockChartData = JsonSerializer.Serialize(stockChartData);

        return View(vm);
    }

    [Authorize(Roles = AppRoles.FinancesAccess)]
    public async Task<IActionResult> Finances()
    {
        var today = DateTime.Today;
        var from = today.AddDays(-29);

        var sales = await _db.Sales
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Product)
            .Where(s => s.SoldAt.Date >= from && s.SoldAt.Date <= today)
            .ToListAsync();

        var byDay = sales
            .GroupBy(s => s.SoldAt.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        var days = Enumerable.Range(0, 30)
            .Select(offset =>
            {
                var day = from.AddDays(offset);
                byDay.TryGetValue(day, out var daySales);
                daySales ??= [];

                var ca = daySales.SelectMany(s => s.Lines).Sum(l => l.UnitPrice * l.Quantity);
                var marge = daySales.SelectMany(s => s.Lines)
                    .Sum(l => (l.UnitPrice - (l.Product?.PurchasePrice ?? 0m)) * l.Quantity);
                var nb = daySales.Count;

                return new DashboardFinanceDayRow
                {
                    Date = day,
                    Ca = ca,
                    MargeBrute = marge,
                    NbVentes = nb,
                    PanierMoyen = nb > 0 ? ca / nb : 0m
                };
            })
            .OrderByDescending(d => d.Date)
            .ToList();

        var paymentBreakdown = Enum.GetValues<PaymentMethod>()
            .Select(method =>
            {
                var methodSales = sales.Where(s => s.PaymentMethod == method).ToList();
                return new DashboardPaymentBreakdownRow
                {
                    PaymentMethod = method,
                    SaleCount = methodSales.Count,
                    Total = methodSales.SelectMany(s => s.Lines).Sum(l => l.UnitPrice * l.Quantity)
                };
            })
            .ToList();

        var vm = new DashboardFinancesViewModel
        {
            From = from,
            To = today,
            Days = days,
            PaymentBreakdown = paymentBreakdown,
            TotalCa = days.Sum(d => d.Ca),
            TotalMarge = days.Sum(d => d.MargeBrute),
            TotalVentes = days.Sum(d => d.NbVentes)
        };

        return View(vm);
    }
}
