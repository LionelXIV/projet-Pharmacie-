using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Reporting;
using Pharmacie.Services;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.ReportsAccess)]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public ReportsController(ApplicationDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public IActionResult Index()
    {
        ViewBag.ExpirationHorizonDays =
            _configuration.GetValue<int>("Alerts:ExpirationHorizonDays", 90);
        return View();
    }

    public async Task<IActionResult> StockStatus()
    {
        var rows = await LoadStockStatusRowsAsync();
        return View(rows);
    }

    public async Task<IActionResult> StockStatusCsv()
    {
        var rows = await LoadStockStatusRowsAsync();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',',
            ReportCsvFormatter.Escape("Produit"),
            ReportCsvFormatter.Escape("Catégorie"),
            ReportCsvFormatter.Escape("Fournisseur"),
            ReportCsvFormatter.Escape("Stock"),
            ReportCsvFormatter.Escape("Seuil"),
            ReportCsvFormatter.Escape("Statut")));

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                ReportCsvFormatter.Escape(r.ProductName),
                ReportCsvFormatter.Escape(r.CategoryName),
                ReportCsvFormatter.Escape(r.SupplierName),
                ReportCsvFormatter.IntInvariant(r.StockQuantity),
                ReportCsvFormatter.IntInvariant(r.AlertThreshold),
                ReportCsvFormatter.Escape(r.StatusLabel)));
        }

        var bytes = ReportCsvFormatter.ToUtf8BytesWithBom(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", ReportCsvFormatter.FileName("rapport-etat-stock"));
    }

    public async Task<IActionResult> NearExpiration()
    {
        var (rows, horizonDays) = await LoadNearExpirationRowsAsync();
        ViewBag.HorizonDays = horizonDays;
        return View(rows);
    }

    public async Task<IActionResult> NearExpirationCsv()
    {
        var (rows, _) = await LoadNearExpirationRowsAsync();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',',
            ReportCsvFormatter.Escape("Produit"),
            ReportCsvFormatter.Escape("Lot"),
            ReportCsvFormatter.Escape("Quantité restante"),
            ReportCsvFormatter.Escape("Date expiration"),
            ReportCsvFormatter.Escape("Jours restants")));

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                ReportCsvFormatter.Escape(r.ProductName),
                ReportCsvFormatter.Escape(r.LotNumber),
                ReportCsvFormatter.IntInvariant(r.QuantityRemaining),
                ReportCsvFormatter.Escape(r.ExpirationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                ReportCsvFormatter.IntInvariant(r.DaysRemaining)));
        }

        var bytes = ReportCsvFormatter.ToUtf8BytesWithBom(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", ReportCsvFormatter.FileName("rapport-proches-expiration"));
    }

    public async Task<IActionResult> ExpiredProducts()
    {
        var rows = await LoadExpiredProductsRowsAsync();
        return View(rows);
    }

    public async Task<IActionResult> ExpiredProductsCsv()
    {
        var rows = await LoadExpiredProductsRowsAsync();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',',
            ReportCsvFormatter.Escape("Produit"),
            ReportCsvFormatter.Escape("Lot"),
            ReportCsvFormatter.Escape("Quantité restante"),
            ReportCsvFormatter.Escape("Date expiration")));

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                ReportCsvFormatter.Escape(r.ProductName),
                ReportCsvFormatter.Escape(r.LotNumber),
                ReportCsvFormatter.IntInvariant(r.QuantityRemaining),
                ReportCsvFormatter.Escape(r.ExpirationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))));
        }

        var bytes = ReportCsvFormatter.ToUtf8BytesWithBom(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", ReportCsvFormatter.FileName("rapport-produits-expires"));
    }

    public async Task<IActionResult> SalesHistory()
    {
        var rows = await LoadSalesHistoryRowsAsync();
        ViewBag.RowLimit = ReportLimits.MaxSalesRows;
        return View(rows);
    }

    public async Task<IActionResult> SalesHistoryCsv()
    {
        var rows = await LoadSalesHistoryRowsAsync();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',',
            ReportCsvFormatter.Escape("Date vente"),
            ReportCsvFormatter.Escape("N° vente"),
            ReportCsvFormatter.Escape("Nombre de lignes"),
            ReportCsvFormatter.Escape("Total (FCFA)"),
            ReportCsvFormatter.Escape("Moyen de paiement")));

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                ReportCsvFormatter.Escape(r.SoldAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                ReportCsvFormatter.IntInvariant(r.SaleId),
                ReportCsvFormatter.IntInvariant(r.LineCount),
                ReportCsvFormatter.FcfaCsvAmount(r.Total),
                ReportCsvFormatter.Escape(PaymentMethodDisplay.GetName(r.PaymentMethod))));
        }

        var bytes = ReportCsvFormatter.ToUtf8BytesWithBom(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", ReportCsvFormatter.FileName("rapport-historique-ventes"));
    }

    public async Task<IActionResult> StockMovementsHistory()
    {
        var rows = await LoadStockMovementsHistoryRowsAsync();
        ViewBag.RowLimit = ReportLimits.MaxMovementRows;
        return View(rows);
    }

    public async Task<IActionResult> StockMovementsHistoryCsv()
    {
        var rows = await LoadStockMovementsHistoryRowsAsync();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',',
            ReportCsvFormatter.Escape("Date"),
            ReportCsvFormatter.Escape("Produit"),
            ReportCsvFormatter.Escape("Type"),
            ReportCsvFormatter.Escape("Quantité"),
            ReportCsvFormatter.Escape("Responsable"),
            ReportCsvFormatter.Escape("N° vente"),
            ReportCsvFormatter.Escape("Motif")));

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                ReportCsvFormatter.Escape(r.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                ReportCsvFormatter.Escape(r.ProductName),
                ReportCsvFormatter.Escape(MovementTypeLabel(r.Type)),
                ReportCsvFormatter.IntInvariant(r.Quantity),
                ReportCsvFormatter.Escape(r.UserOrResponsible),
                r.SaleId.HasValue ? ReportCsvFormatter.IntInvariant(r.SaleId.Value) : "",
                ReportCsvFormatter.Escape(r.Reason ?? "")));
        }

        var bytes = ReportCsvFormatter.ToUtf8BytesWithBom(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", ReportCsvFormatter.FileName("rapport-historique-mouvements"));
    }

    private static string MovementTypeLabel(StockMovementType t) => t switch
    {
        StockMovementType.Entree => "Entrée",
        StockMovementType.Sortie => "Sortie",
        StockMovementType.Ajustement => "Ajustement",
        _ => t.ToString()
    };

    private async Task<List<ReportStockStatusRowViewModel>> LoadStockStatusRowsAsync()
    {
        var products = await _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Where(p => p.IsActive)
            .OrderBy(p => p.CommercialName)
            .ToListAsync();

        return products.Select(p =>
        {
            string label;
            string badge;
            if (p.StockQuantity == 0)
            {
                label = "Rupture";
                badge = "bg-danger";
            }
            else if (p.StockQuantity <= p.AlertThreshold)
            {
                label = "Stock faible";
                badge = "bg-warning text-dark";
            }
            else
            {
                label = "Normal";
                badge = "bg-success";
            }

            return new ReportStockStatusRowViewModel
            {
                ProductName = p.CommercialName,
                CategoryName = p.Category?.Name ?? "—",
                SupplierName = p.Supplier?.Name ?? "—",
                StockQuantity = p.StockQuantity,
                AlertThreshold = p.AlertThreshold,
                StatusLabel = label,
                StatusBadgeClass = badge
            };
        }).ToList();
    }

    private async Task<(List<ReportNearExpirationRowViewModel> Rows, int HorizonDays)> LoadNearExpirationRowsAsync()
    {
        var horizon = _configuration.GetValue<int>("Alerts:ExpirationHorizonDays", 90);
        var today = DateTime.Today;
        var horizonEnd = today.AddDays(horizon);

        var lots = await _db.ProductBatches
            .AsNoTracking()
            .Include(b => b.Product)
            .Where(b =>
                b.Quantity > 0
                && b.ExpirationDate.Date >= today
                && b.ExpirationDate.Date <= horizonEnd)
            .OrderBy(b => b.ExpirationDate)
            .ThenBy(b => b.Product!.CommercialName)
            .ToListAsync();

        var rows = lots.Select(b =>
        {
            var exp = b.ExpirationDate.Date;
            var days = (exp - today).Days;
            return new ReportNearExpirationRowViewModel
            {
                ProductName = b.Product?.CommercialName ?? $"#{b.ProductId}",
                LotNumber = b.LotNumber,
                QuantityRemaining = b.Quantity,
                ExpirationDate = exp,
                DaysRemaining = days
            };
        }).ToList();

        return (rows, horizon);
    }

    private async Task<List<ReportExpiredLotRowViewModel>> LoadExpiredProductsRowsAsync()
    {
        var today = DateTime.Today;

        var lots = await _db.ProductBatches
            .AsNoTracking()
            .Include(b => b.Product)
            .Where(b => b.Quantity > 0 && b.ExpirationDate.Date < today)
            .OrderBy(b => b.ExpirationDate)
            .ThenBy(b => b.Product!.CommercialName)
            .ToListAsync();

        return lots.Select(b => new ReportExpiredLotRowViewModel
        {
            ProductName = b.Product?.CommercialName ?? $"#{b.ProductId}",
            LotNumber = b.LotNumber,
            QuantityRemaining = b.Quantity,
            ExpirationDate = b.ExpirationDate.Date
        }).ToList();
    }

    private async Task<List<ReportSaleHistoryRowViewModel>> LoadSalesHistoryRowsAsync()
    {
        var sales = await _db.Sales
            .AsNoTracking()
            .Include(s => s.Lines)
            .OrderByDescending(s => s.SoldAt)
            .ThenByDescending(s => s.Id)
            .Take(ReportLimits.MaxSalesRows)
            .ToListAsync();

        return sales.Select(s => new ReportSaleHistoryRowViewModel
        {
            SaleId = s.Id,
            SoldAt = s.SoldAt,
            LineCount = s.Lines.Count,
            Total = s.Lines.Sum(l => l.Quantity * l.UnitPrice),
            PaymentMethod = s.PaymentMethod
        }).ToList();
    }

    private async Task<List<ReportMovementHistoryRowViewModel>> LoadStockMovementsHistoryRowsAsync()
    {
        var movements = await _db.StockMovements
            .AsNoTracking()
            .Include(m => m.Product)
            .Include(m => m.Batch)
            .OrderByDescending(m => m.OccurredAt)
            .ThenByDescending(m => m.Id)
            .Take(ReportLimits.MaxMovementRows)
            .ToListAsync();

        var labelsByUserId = await UserDisplayResolver.LoadLabelsByIdAsync(_db, movements.Select(m => m.UserId));

        return movements.Select(m => new ReportMovementHistoryRowViewModel
        {
            OccurredAt = m.OccurredAt,
            ProductName = m.Product?.CommercialName ?? $"#{m.ProductId}",
            Type = m.Type,
            Quantity = m.Quantity,
            UserOrResponsible = UserDisplayResolver.Resolve(labelsByUserId, m.UserId),
            SaleId = m.SaleId,
            Reason = m.Reason
        }).ToList();
    }
}
