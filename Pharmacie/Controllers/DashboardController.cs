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

[Authorize]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public DashboardController(ApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    [Authorize(Roles = AppRoles.DashboardAccess)]
    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        var horizon = _configuration.GetValue<int>("Alerts:ExpirationHorizonDays", 90);
        var horizonEnd = today.AddDays(horizon);

        var todaySalesRaw = await _db.Sales
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Product!)
            .ThenInclude(p => p.Category)
            .Where(s => !s.IsAnnulee && !s.IsAdminTest && s.SoldAt.Date == today)
            .ToListAsync();

        var todaySalesOff = ProduitsExtrasFilter.VentesAvecLignesOfficielles(todaySalesRaw).ToList();
        var todayLignesOff = todaySalesRaw.SelectMany(s => ProduitsExtrasFilter.LignesOfficielles(s.Lines)).ToList();

        var caDuJour = todayLignesOff.Sum(l => l.UnitPrice * l.Quantity);

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
            SalesTodayCount = todaySalesOff.Count,
            SalesTodayTotal = caDuJour
        };

        if (AppRoles.CanAccessFinances(User))
        {
            var margeBrute = todayLignesOff
                .Sum(l => (l.UnitPrice - (l.Product?.PurchasePrice ?? 0m)) * l.Quantity);
            var nbVentes = todaySalesOff.Count;
            var panierMoyen = nbVentes > 0 ? caDuJour / nbVentes : 0m;

            ViewBag.ShowFinances = true;
            ViewBag.MargeBrute = margeBrute;
            ViewBag.PanierMoyen = panierMoyen;
            ViewBag.NbVentes = nbVentes;
            ViewBag.CaDuJour = caDuJour;

            // Graphiques finances — 30 derniers jours
            var debutFinance = today.AddDays(-30);
            var finExclusiveFinance = today.AddDays(1);

            var ventes30jRaw = await _db.Sales
                .AsNoTracking()
                .Include(s => s.Lines)
                    .ThenInclude(l => l.Product!)
                        .ThenInclude(p => p.Category)
                .Where(s => !s.IsAnnulee && !s.IsAdminTest && s.SoldAt >= debutFinance && s.SoldAt < finExclusiveFinance)
                .ToListAsync();

            var caParCategorie = ventes30jRaw
                .SelectMany(s => ProduitsExtrasFilter.LignesOfficielles(s.Lines))
                .GroupBy(l => l.Product?.Category?.Name ?? "Sans catégorie")
                .Select(g => new
                {
                    Categorie = g.Key,
                    CA = g.Sum(l => l.UnitPrice * l.Quantity)
                })
                .OrderByDescending(x => x.CA)
                .Take(8)
                .ToList();

            ViewBag.CategorieLabels = JsonSerializer.Serialize(caParCategorie.Select(x => x.Categorie).ToList());
            ViewBag.CategorieData = JsonSerializer.Serialize(caParCategorie.Select(x => x.CA).ToList());

            var bonsTotal = await _db.Bons
                .AsNoTracking()
                .Where(b => b.DateCreation >= debutFinance && b.DateCreation < finExclusiveFinance)
                .SumAsync(b => (decimal?)b.MontantTotal) ?? 0m;

            var avoirsTotal = await _db.Avoirs
                .AsNoTracking()
                .Where(a => a.DateCreation >= debutFinance && a.DateCreation < finExclusiveFinance)
                .SumAsync(a => (decimal?)a.MontantTotal) ?? 0m;

            static decimal SumPm(IEnumerable<Sale> ventes, PaymentMethod pm) =>
                ProduitsExtrasFilter.CaOfficiel(
                    ventes.Where(s => s.PaymentMethod == pm).SelectMany(s => s.Lines));

            var caEspeces = SumPm(ventes30jRaw, PaymentMethod.Especes);
            var caWave = SumPm(ventes30jRaw, PaymentMethod.Wave);
            var caOm = SumPm(ventes30jRaw, PaymentMethod.OrangeMoney);
            var caAutres = ProduitsExtrasFilter.CaOfficiel(
                ventes30jRaw
                    .Where(s => s.PaymentMethod is not (PaymentMethod.Especes or PaymentMethod.Wave or PaymentMethod.OrangeMoney))
                    .SelectMany(s => s.Lines));

            ViewBag.PaiementLabels = JsonSerializer.Serialize(
                new[] { "Espèces", "Wave", "Orange Money", "Bon/Crédit", "Avoir", "Autres" });
            ViewBag.PaiementData = JsonSerializer.Serialize(
                new[] { caEspeces, caWave, caOm, bonsTotal, avoirsTotal, caAutres });
        }
        else
        {
            ViewBag.CategorieLabels = "[]";
            ViewBag.CategorieData = "[]";
            ViewBag.PaiementLabels = "[]";
            ViewBag.PaiementData = "[]";
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
            .Include(s => s.Lines).ThenInclude(l => l.Product!).ThenInclude(p => p.Category)
            .Where(s => !s.IsAnnulee && !s.IsAdminTest && s.SoldAt.Date >= chartStart && s.SoldAt.Date <= today)
            .ToListAsync();

        var salesByDay = salesInRange
            .GroupBy(s => s.SoldAt.Date)
            .ToDictionary(
                g => g.Key,
                g => ProduitsExtrasFilter.CaOfficiel(g.SelectMany(s => s.Lines)));

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
            .Where(p => p.IsActive && (p.Category == null || !p.Category.EstHorsSysteme))
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
            .ThenInclude(l => l.Product!)
            .ThenInclude(p => p.Category)
            .Where(s => !s.IsAnnulee && !s.IsAdminTest && s.SoldAt.Date >= from && s.SoldAt.Date <= today)
            .ToListAsync();

        var byDay = sales
            .GroupBy(s => s.SoldAt.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Uniquement les jours avec au moins une vente officielle (pas de lignes à 0)
        var days = byDay
            .Select(kv =>
            {
                var day = kv.Key;
                var daySales = kv.Value;

                var lignesOff = daySales.SelectMany(s => ProduitsExtrasFilter.LignesOfficielles(s.Lines)).ToList();
                var ca = lignesOff.Sum(l => l.UnitPrice * l.Quantity);
                var marge = lignesOff
                    .Sum(l => (l.UnitPrice - (l.Product?.PurchasePrice ?? 0m)) * l.Quantity);
                var nb = ProduitsExtrasFilter.VentesAvecLignesOfficielles(daySales).Count();

                return new DashboardFinanceDayRow
                {
                    Date = day,
                    Ca = ca,
                    MargeBrute = marge,
                    NbVentes = nb,
                    PanierMoyen = nb > 0 ? ca / nb : 0m
                };
            })
            .Where(d => d.NbVentes > 0)
            .OrderByDescending(d => d.Date)
            .ToList();

        var paymentBreakdown = Enum.GetValues<PaymentMethod>()
            .Select(method =>
            {
                var methodSales = sales.Where(s => s.PaymentMethod == method).ToList();
                return new DashboardPaymentBreakdownRow
                {
                    PaymentMethod = method,
                    SaleCount = ProduitsExtrasFilter.VentesAvecLignesOfficielles(methodSales).Count(),
                    Total = ProduitsExtrasFilter.CaOfficiel(methodSales.SelectMany(s => s.Lines))
                };
            })
            .ToList();

        static decimal SumPm(IEnumerable<Sale> ventes, PaymentMethod pm) =>
            ProduitsExtrasFilter.CaOfficiel(
                ventes.Where(s => s.PaymentMethod == pm).SelectMany(s => s.Lines));

        var caEspeces = SumPm(sales, PaymentMethod.Especes);
        var caWave = SumPm(sales, PaymentMethod.Wave);
        var caOm = SumPm(sales, PaymentMethod.OrangeMoney);
        var caAutres = ProduitsExtrasFilter.CaOfficiel(
            sales.Where(s => s.PaymentMethod is not (PaymentMethod.Especes or PaymentMethod.Wave or PaymentMethod.OrangeMoney))
                .SelectMany(s => s.Lines));

        var bonsTotal = await _db.Bons
            .AsNoTracking()
            .Where(b => b.DateCreation.Date >= from && b.DateCreation.Date <= today)
            .SumAsync(b => (decimal?)b.MontantTotal) ?? 0m;

        var avoirsTotal = await _db.Avoirs
            .AsNoTracking()
            .Where(a => a.DateCreation.Date >= from && a.DateCreation.Date <= today)
            .SumAsync(a => (decimal?)a.MontantTotal) ?? 0m;

        ViewBag.NbJours = 30;
        ViewBag.PaiementLabels = JsonSerializer.Serialize(
            new[] { "Espèces", "Wave", "Orange Money", "Bon/Crédit", "Avoir", "Autres" });
        ViewBag.PaiementData = JsonSerializer.Serialize(
            new[] { caEspeces, caWave, caOm, bonsTotal, avoirsTotal, caAutres });

        var debut = DateTime.Today.AddDays(-30);
        var ventesParJour = await _db.Sales
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Product!)
            .ThenInclude(p => p.Category)
            .Where(s => !s.IsAnnulee && !s.IsAdminTest && s.SoldAt.Date >= debut)
            .ToListAsync();

        var tableauJournalier = ventesParJour
            .GroupBy(s => s.SoldAt.Date)
            .Select(g =>
            {
                var (exonere, ht, tva, ttc) = TVACalculator.CalculerTVAJournee(g);
                return new
                {
                    Date = g.Key,
                    Exonere = exonere,
                    HT = ht,
                    TVA = tva,
                    TTC = ttc,
                };
            })
            .Where(d => d.TTC != 0 || d.Exonere != 0 || d.HT != 0 || d.TVA != 0)
            .OrderByDescending(d => d.Date)
            .ToList();

        ViewBag.TableauJournalier = JsonSerializer.Serialize(tableauJournalier);

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
