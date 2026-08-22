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

    [Authorize(Roles = AppRoles.StockReportsAccess)]
    public async Task<IActionResult> StockStatus()
    {
        var rows = await LoadStockStatusRowsAsync();
        await PopulateStockStatusKpisAsync();
        return View(rows);
    }

    [Authorize(Roles = AppRoles.StockReportsAccess)]
    public async Task<IActionResult> ImprimerStockStatus()
    {
        var rows = await LoadStockStatusRowsAsync();
        await PopulateStockStatusKpisAsync();
        return View("StockStatusPrint", rows);
    }

    [Authorize(Roles = AppRoles.StockReportsAccess)]
    public async Task<IActionResult> StockStatusCsv()
    {
        var rows = await LoadStockStatusRowsAsync();
        var sb = ReportCsvFormatter.CreateBuilder();
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Produit"),
            ReportCsvFormatter.Escape("Catégorie"),
            ReportCsvFormatter.Escape("Fournisseur"),
            ReportCsvFormatter.Escape("Stock"),
            ReportCsvFormatter.Escape("Seuil"),
            ReportCsvFormatter.Escape("Statut")));

        foreach (var r in rows)
        {
            sb.AppendLine(ReportCsvFormatter.Join(
                ReportCsvFormatter.Escape(r.ProductName),
                ReportCsvFormatter.Escape(r.CategoryName),
                ReportCsvFormatter.Escape(r.SupplierName),
                ReportCsvFormatter.IntInvariant(r.StockQuantity),
                ReportCsvFormatter.IntInvariant(r.AlertThreshold),
                ReportCsvFormatter.Escape(r.StatusLabel)));
        }

        return ReportCsvFormatter.FileResult(this, sb.ToString(), "rapport-etat-stock");
    }

    [Authorize(Roles = AppRoles.StockReportsAccess)]
    public async Task<IActionResult> NearExpiration()
    {
        var (rows, horizonDays) = await LoadNearExpirationRowsAsync();
        ViewBag.HorizonDays = horizonDays;
        return View(rows);
    }

    [Authorize(Roles = AppRoles.StockReportsAccess)]
    public async Task<IActionResult> NearExpirationCsv()
    {
        var (rows, _) = await LoadNearExpirationRowsAsync();
        var sb = ReportCsvFormatter.CreateBuilder();
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Produit"),
            ReportCsvFormatter.Escape("Lot"),
            ReportCsvFormatter.Escape("Quantité restante"),
            ReportCsvFormatter.Escape("Date expiration"),
            ReportCsvFormatter.Escape("Jours restants")));

        foreach (var r in rows)
        {
            sb.AppendLine(ReportCsvFormatter.Join(
                ReportCsvFormatter.Escape(r.ProductName),
                ReportCsvFormatter.Escape(r.LotNumber),
                ReportCsvFormatter.IntInvariant(r.QuantityRemaining),
                ReportCsvFormatter.Escape(r.ExpirationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                ReportCsvFormatter.IntInvariant(r.DaysRemaining)));
        }

        return ReportCsvFormatter.FileResult(this, sb.ToString(), "rapport-proches-expiration");
    }

    [Authorize(Roles = AppRoles.StockReportsAccess)]
    public async Task<IActionResult> ExpiredProducts()
    {
        var rows = await LoadExpiredProductsRowsAsync();
        return View(rows);
    }

    [Authorize(Roles = AppRoles.StockReportsAccess)]
    public async Task<IActionResult> ExpiredProductsCsv()
    {
        var rows = await LoadExpiredProductsRowsAsync();
        var sb = ReportCsvFormatter.CreateBuilder();
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Produit"),
            ReportCsvFormatter.Escape("Lot"),
            ReportCsvFormatter.Escape("Quantité restante"),
            ReportCsvFormatter.Escape("Date expiration")));

        foreach (var r in rows)
        {
            sb.AppendLine(ReportCsvFormatter.Join(
                ReportCsvFormatter.Escape(r.ProductName),
                ReportCsvFormatter.Escape(r.LotNumber),
                ReportCsvFormatter.IntInvariant(r.QuantityRemaining),
                ReportCsvFormatter.Escape(r.ExpirationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))));
        }

        return ReportCsvFormatter.FileResult(this, sb.ToString(), "rapport-produits-expires");
    }

    [Authorize(Roles = AppRoles.OperationalReportsAccess)]
    public async Task<IActionResult> SalesHistory()
    {
        var rows = await LoadSalesHistoryRowsAsync();
        ViewBag.RowLimit = ReportLimits.MaxSalesRows;
        return View(rows);
    }

    [Authorize(Roles = AppRoles.OperationalReportsAccess)]
    public async Task<IActionResult> SalesHistoryCsv()
    {
        var rows = await LoadSalesHistoryRowsAsync();
        var sb = ReportCsvFormatter.CreateBuilder();
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Date vente"),
            ReportCsvFormatter.Escape("N° vente"),
            ReportCsvFormatter.Escape("Nombre de lignes"),
            ReportCsvFormatter.Escape("Total (FCFA)"),
            ReportCsvFormatter.Escape("Moyen de paiement")));

        foreach (var r in rows)
        {
            sb.AppendLine(ReportCsvFormatter.Join(
                ReportCsvFormatter.Escape(r.SoldAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                ReportCsvFormatter.IntInvariant(r.SaleId),
                ReportCsvFormatter.IntInvariant(r.LineCount),
                ReportCsvFormatter.FcfaCsvAmount(r.Total),
                ReportCsvFormatter.Escape(PaymentMethodDisplay.GetName(r.PaymentMethod))));
        }

        return ReportCsvFormatter.FileResult(this, sb.ToString(), "rapport-historique-ventes");
    }

    [Authorize(Roles = AppRoles.OperationalReportsAccess)]
    public async Task<IActionResult> StockMovementsHistory()
    {
        var rows = await LoadStockMovementsHistoryRowsAsync();
        ViewBag.RowLimit = ReportLimits.MaxMovementRows;
        return View(rows);
    }

    [Authorize(Roles = AppRoles.OperationalReportsAccess)]
    public async Task<IActionResult> StockMovementsHistoryCsv()
    {
        var rows = await LoadStockMovementsHistoryRowsAsync();
        var sb = ReportCsvFormatter.CreateBuilder();
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Date"),
            ReportCsvFormatter.Escape("Produit"),
            ReportCsvFormatter.Escape("Type"),
            ReportCsvFormatter.Escape("Quantité"),
            ReportCsvFormatter.Escape("Responsable"),
            ReportCsvFormatter.Escape("N° vente"),
            ReportCsvFormatter.Escape("Motif")));

        foreach (var r in rows)
        {
            sb.AppendLine(ReportCsvFormatter.Join(
                ReportCsvFormatter.Escape(r.OccurredAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
                ReportCsvFormatter.Escape(r.ProductName),
                ReportCsvFormatter.Escape(MovementTypeLabel(r.Type)),
                ReportCsvFormatter.IntInvariant(r.Quantity),
                ReportCsvFormatter.Escape(r.UserOrResponsible),
                r.SaleId.HasValue ? ReportCsvFormatter.IntInvariant(r.SaleId.Value) : "",
                ReportCsvFormatter.Escape(r.Reason ?? "")));
        }

        return ReportCsvFormatter.FileResult(this, sb.ToString(), "rapport-historique-mouvements");
    }

    private static string MovementTypeLabel(StockMovementType t) => t switch
    {
        StockMovementType.Entree => "Entrée",
        StockMovementType.Sortie => "Sortie",
        StockMovementType.Ajustement => "Ajustement",
        _ => t.ToString()
    };

    private async Task PopulateStockStatusKpisAsync()
    {
        var produitsValorises = _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive
                && p.StockQuantity > 0
                && (p.Category == null || !p.Category.EstHorsSysteme));

        var valeurStockPA = await produitsValorises
            .SumAsync(p => (decimal?)(p.PurchasePrice * p.StockQuantity)) ?? 0m;

        var valeurStockPV = await produitsValorises
            .SumAsync(p => (decimal?)(p.SalePrice * p.StockQuantity)) ?? 0m;

        var nbProduitsEnStock = await _db.Products
            .AsNoTracking()
            .CountAsync(p => p.IsActive && p.StockQuantity > 0);

        var nbRupture = await _db.Products
            .AsNoTracking()
            .CountAsync(p => p.IsActive && p.StockQuantity <= 0);

        ViewBag.ValeurStockPA = valeurStockPA;
        ViewBag.ValeurStockPV = valeurStockPV;
        ViewBag.MargePotentielle = valeurStockPV - valeurStockPA;
        ViewBag.NbProduitsEnStock = nbProduitsEnStock;
        ViewBag.NbRupture = nbRupture;
    }

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
            .Include(s => s.Lines).ThenInclude(l => l.Product!).ThenInclude(p => p.Category)
            .Where(s => !s.IsAnnulee && !s.IsAdminTest)
            .OrderByDescending(s => s.SoldAt)
            .ThenByDescending(s => s.Id)
            .Take(ReportLimits.MaxSalesRows)
            .ToListAsync();

        return sales.Select(s =>
        {
            var lignesOff = ProduitsExtrasFilter.LignesOfficielles(s.Lines).ToList();
            return new ReportSaleHistoryRowViewModel
            {
                SaleId = s.Id,
                SoldAt = s.SoldAt,
                LineCount = lignesOff.Count,
                Total = lignesOff.Sum(l => l.Quantity * l.UnitPrice),
                PaymentMethod = s.PaymentMethod
            };
        }).ToList();
    }

    [Authorize(Roles = AppRoles.FinancesAccess)]
    public async Task<IActionResult> EtatTVA(DateTime? dateDebut = null, DateTime? dateFin = null)
    {
        var vm = await BuildEtatTVAAsync(dateDebut, dateFin);
        return View(vm);
    }

    /// <summary>Page imprimable (export PDF via impression navigateur).</summary>
    [Authorize(Roles = AppRoles.FinancesAccess)]
    public async Task<IActionResult> ExportTVAPdf(DateTime? dateDebut = null, DateTime? dateFin = null)
    {
        var vm = await BuildEtatTVAAsync(dateDebut, dateFin);
        return View("EtatTVAPrint", vm);
    }

    [Authorize(Roles = AppRoles.FinancesAccess)]
    public async Task<IActionResult> EtatTVAPrint(DateTime? dateDebut = null, DateTime? dateFin = null)
    {
        var vm = await BuildEtatTVAAsync(dateDebut, dateFin);
        return View(vm);
    }

    private async Task<EtatTVAViewModel> BuildEtatTVAAsync(DateTime? dateDebut, DateTime? dateFin)
    {
        var debut = (dateDebut ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1)).Date;
        var fin = (dateFin ?? DateTime.Today).Date;
        if (fin < debut)
            (debut, fin) = (fin, debut);

        // Garde-fou : période max 366 jours
        if ((fin - debut).TotalDays > 366)
            fin = debut.AddDays(366);

        var finExclusive = fin.AddDays(1);

        var ventes = await _db.Sales
            .AsNoTracking()
            .Include(s => s.Lines).ThenInclude(l => l.Product!).ThenInclude(p => p.Category)
            .Where(s => !s.IsAnnulee && !s.IsAdminTest && s.SoldAt >= debut && s.SoldAt < finExclusive)
            .ToListAsync();

        var lignesParJour = ventes
            .GroupBy(s => s.SoldAt.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var (exonere, ht, tva, ttc) = TVACalculator.CalculerTVAJournee(g);
                return new EtatTVALigneViewModel
                {
                    Date = g.Key,
                    MontantExonere = exonere,
                    MontantHT = ht,
                    MontantTVA = tva,
                    MontantTTC = ttc
                };
            })
            .ToList();

        var joursComplets = new List<EtatTVALigneViewModel>();
        for (var day = debut; day <= fin; day = day.AddDays(1))
        {
            var existing = lignesParJour.FirstOrDefault(l => l.Date == day);
            joursComplets.Add(existing ?? new EtatTVALigneViewModel { Date = day });
        }

        return new EtatTVAViewModel
        {
            DateDebut = debut,
            DateFin = fin,
            Lignes = joursComplets,
            TotalExonere = joursComplets.Sum(l => l.MontantExonere),
            TotalHT = joursComplets.Sum(l => l.MontantHT),
            TotalTVA = joursComplets.Sum(l => l.MontantTVA),
            TotalTTC = joursComplets.Sum(l => l.MontantTTC)
        };
    }

    [Authorize(Roles = AppRoles.FinancesAccess)]
    public async Task<IActionResult> RapportCA(DateTime? dateDebut = null, DateTime? dateFin = null)
    {
        var vm = await BuildRapportCAAsync(dateDebut, dateFin);
        return View(vm);
    }

    [Authorize(Roles = AppRoles.FinancesAccess)]
    public async Task<IActionResult> ImprimerRapportCA(DateTime? dateDebut = null, DateTime? dateFin = null)
    {
        var vm = await BuildRapportCAAsync(dateDebut, dateFin);
        return View("RapportCAPrint", vm);
    }

    [Authorize(Roles = AppRoles.FinancesAccess)]
    public async Task<IActionResult> ExportRapportCACSV(DateTime? dateDebut = null, DateTime? dateFin = null)
    {
        var vm = await BuildRapportCAAsync(dateDebut, dateFin);
        var sb = ReportCsvFormatter.CreateBuilder();

        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Rapport CA"),
            ReportCsvFormatter.Escape($"{vm.DateDebut:yyyy-MM-dd} → {vm.DateFin:yyyy-MM-dd}")));
        sb.AppendLine();

        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Indicateur"),
            ReportCsvFormatter.Escape("Valeur")));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("CA Total (FCFA)"), ReportCsvFormatter.FcfaCsvAmount(vm.CATotal)));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("Prix d'achat total PA (FCFA)"), ReportCsvFormatter.FcfaCsvAmount(vm.PATotal)));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("Marge brute (FCFA)"), ReportCsvFormatter.FcfaCsvAmount(vm.MargeBrute)));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("Taux de marge (%)"), ReportCsvFormatter.DecimalInvariant(vm.TauxMarge)));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("Panier moyen (FCFA)"), ReportCsvFormatter.FcfaCsvAmount(vm.PanierMoyen)));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("Nb ventes"), ReportCsvFormatter.IntInvariant(vm.NombreVentes)));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("TVA collectée (FCFA)"), ReportCsvFormatter.FcfaCsvAmount(vm.TVACollectee)));
        sb.AppendLine();

        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Mode de paiement"),
            ReportCsvFormatter.Escape("CA (FCFA)"),
            ReportCsvFormatter.Escape("%")));
        void AppendPm(string label, decimal ca)
        {
            var pct = vm.CATotal > 0 ? ca / vm.CATotal * 100 : 0;
            sb.AppendLine(ReportCsvFormatter.Join(
                ReportCsvFormatter.Escape(label),
                ReportCsvFormatter.FcfaCsvAmount(ca),
                ReportCsvFormatter.DecimalInvariant(pct)));
        }
        AppendPm("Espèces", vm.CAEspeces);
        AppendPm("Wave", vm.CAWave);
        AppendPm("Orange Money", vm.CAOrangeMoney);
        AppendPm("Autres", vm.CAAutres);
        sb.AppendLine();

        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Date"),
            ReportCsvFormatter.Escape("CA (FCFA)"),
            ReportCsvFormatter.Escape("Nb ventes")));
        foreach (var j in vm.CAParJour)
        {
            sb.AppendLine(ReportCsvFormatter.Join(
                ReportCsvFormatter.Escape(j.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                ReportCsvFormatter.FcfaCsvAmount(j.CA),
                ReportCsvFormatter.IntInvariant(j.NbVentes)));
        }
        sb.AppendLine();

        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Vendeur"),
            ReportCsvFormatter.Escape("Nb ventes"),
            ReportCsvFormatter.Escape("CA (FCFA)"),
            ReportCsvFormatter.Escape("Panier moyen (FCFA)")));
        foreach (var v in vm.CAParVendeur)
        {
            sb.AppendLine(ReportCsvFormatter.Join(
                ReportCsvFormatter.Escape(v.NomVendeur),
                ReportCsvFormatter.IntInvariant(v.NbVentes),
                ReportCsvFormatter.FcfaCsvAmount(v.CA),
                ReportCsvFormatter.FcfaCsvAmount(v.PanierMoyen)));
        }

        return ReportCsvFormatter.FileResult(this, sb.ToString(),
            $"rapport-ca_{vm.DateDebut:yyyyMMdd}_{vm.DateFin:yyyyMMdd}");
    }

    private async Task<RapportCAViewModel> BuildRapportCAAsync(DateTime? dateDebut, DateTime? dateFin)
    {
        var debut = (dateDebut ?? DateTime.Today.AddDays(-30)).Date;
        var fin = (dateFin ?? DateTime.Today).Date;
        if (fin < debut)
            (debut, fin) = (fin, debut);

        var start = debut;
        var endExclusive = fin.AddDays(1);

        var ventes = await _db.Sales
            .AsNoTracking()
            .Include(s => s.Lines).ThenInclude(l => l.Product!).ThenInclude(p => p.Category)
            .Include(s => s.Vendeur)
            .Where(s => !s.IsAnnulee && !s.IsAdminTest && s.SoldAt >= start && s.SoldAt < endExclusive)
            .OrderBy(s => s.SoldAt)
            .ToListAsync();

        static decimal LineCa(SaleLine l) => l.UnitPrice * l.Quantity;
        static decimal LinePa(SaleLine l) => (l.Product?.PurchasePrice ?? 0m) * l.Quantity;

        IEnumerable<SaleLine> LignesOff(IEnumerable<Sale> sales) =>
            sales.SelectMany(s => ProduitsExtrasFilter.LignesOfficielles(s.Lines));

        decimal SumPm(PaymentMethod pm) =>
            LignesOff(ventes.Where(s => s.PaymentMethod == pm)).Sum(LineCa);

        var lignesOfficielles = LignesOff(ventes).ToList();
        var ventesOfficielles = ProduitsExtrasFilter.VentesAvecLignesOfficielles(ventes).ToList();

        var caTotal = lignesOfficielles.Sum(LineCa);
        var paTotal = lignesOfficielles.Sum(LinePa);
        var marge = caTotal - paTotal;
        var caEspeces = SumPm(PaymentMethod.Especes);
        var caWave = SumPm(PaymentMethod.Wave);
        var caOm = SumPm(PaymentMethod.OrangeMoney);
        var caAutres = LignesOff(ventes.Where(s =>
                s.PaymentMethod is not (PaymentMethod.Especes or PaymentMethod.Wave or PaymentMethod.OrangeMoney)))
            .Sum(LineCa);

        var tvaCollectee = lignesOfficielles
            .Sum(l => TVACalculator.CalculerTVA(l.Product, l.UnitPrice, l.Quantity).MontantTVA);

        return new RapportCAViewModel
        {
            DateDebut = debut,
            DateFin = fin,
            NombreVentes = ventesOfficielles.Count,
            CATotal = caTotal,
            PATotal = paTotal,
            MargeBrute = marge,
            TauxMarge = caTotal > 0 ? marge / caTotal * 100 : 0,
            PanierMoyen = ventesOfficielles.Count > 0 ? caTotal / ventesOfficielles.Count : 0,
            TVACollectee = tvaCollectee,
            CAEspeces = caEspeces,
            CAWave = caWave,
            CAOrangeMoney = caOm,
            CAAutres = caAutres,
            CAParJour = ventes
                .GroupBy(s => s.SoldAt.Date)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var gOff = ProduitsExtrasFilter.VentesAvecLignesOfficielles(g).ToList();
                    var ca = LignesOff(g).Sum(LineCa);
                    return new CAJourViewModel
                    {
                        Date = g.Key,
                        CA = ca,
                        NbVentes = gOff.Count
                    };
                })
                .Where(j => j.NbVentes > 0 || j.CA > 0)
                .ToList(),
            CAParVendeur = ventesOfficielles
                .GroupBy(s => new
                {
                    s.VendeurId,
                    Nom = s.Vendeur != null ? s.Vendeur.Nom : "Non attribué"
                })
                .Select(g =>
                {
                    var ca = LignesOff(g).Sum(LineCa);
                    var nb = g.Count();
                    return new CAVendeurViewModel
                    {
                        NomVendeur = g.Key.Nom,
                        CA = ca,
                        NbVentes = nb
                    };
                })
                .OrderByDescending(v => v.CA)
                .ToList()
        };
    }

    [Authorize(Roles = AppRoles.FinancesAccess)]
    public async Task<IActionResult> VendeursDuJour(DateTime? date = null)
    {
        var targetDate = (date ?? DateTime.Today).Date;
        var rapport = await BuildVendeurRapportAsync(targetDate);
        ViewBag.DateRapport = targetDate;
        ViewBag.TotalJour = rapport.Sum(r => r.ChiffreAffaires);
        ViewBag.TotalVentes = rapport.Sum(r => r.NombreVentes);
        return View(rapport);
    }

    [Authorize(Roles = AppRoles.FinancesAccess)]
    public async Task<IActionResult> ImprimerRapportVendeurs(DateTime? date = null)
    {
        var targetDate = (date ?? DateTime.Today).Date;
        var rapport = await BuildVendeurRapportAsync(targetDate);
        ViewBag.DateRapport = targetDate;
        ViewBag.TotalJour = rapport.Sum(r => r.ChiffreAffaires);
        ViewBag.TotalVentes = rapport.Sum(r => r.NombreVentes);
        return View("RapportVendeursPrint", rapport);
    }

    [Authorize(Roles = AppRoles.FinancesAccess)]
    public async Task<IActionResult> ExportVendeursCsv(DateTime? date = null)
    {
        var targetDate = (date ?? DateTime.Today).Date;
        var rapport = await BuildVendeurRapportAsync(targetDate);
        var sb = ReportCsvFormatter.CreateBuilder();
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Vendeur"),
            ReportCsvFormatter.Escape("Couleur ticket"),
            ReportCsvFormatter.Escape("Nb ventes"),
            ReportCsvFormatter.Escape("CA (FCFA)"),
            ReportCsvFormatter.Escape("Articles"),
            ReportCsvFormatter.Escape("Panier moyen (FCFA)")));

        foreach (var r in rapport)
        {
            sb.AppendLine(ReportCsvFormatter.Join(
                ReportCsvFormatter.Escape(r.NomVendeur),
                ReportCsvFormatter.Escape(r.CouleurTicket ?? ""),
                ReportCsvFormatter.IntInvariant(r.NombreVentes),
                ReportCsvFormatter.FcfaCsvAmount(r.ChiffreAffaires),
                ReportCsvFormatter.IntInvariant(r.NombreArticles),
                ReportCsvFormatter.FcfaCsvAmount(r.PanierMoyen)));
        }

        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("TOTAL"),
            "",
            ReportCsvFormatter.IntInvariant(rapport.Sum(r => r.NombreVentes)),
            ReportCsvFormatter.FcfaCsvAmount(rapport.Sum(r => r.ChiffreAffaires)),
            ReportCsvFormatter.IntInvariant(rapport.Sum(r => r.NombreArticles)),
            ""));

        return ReportCsvFormatter.FileResult(this, sb.ToString(), $"performance_vendeurs_{targetDate:yyyyMMdd}");
    }

    private async Task<List<VendeurRapportViewModel>> BuildVendeurRapportAsync(DateTime targetDate)
    {
        var start = targetDate.Date;
        var end = start.AddDays(1);
        var sales = await _db.Sales
            .AsNoTracking()
            .Include(s => s.Lines).ThenInclude(l => l.Product!).ThenInclude(p => p.Category)
            .Include(s => s.Vendeur)
            .Where(s => !s.IsAnnulee && !s.IsAdminTest && s.SoldAt >= start && s.SoldAt < end)
            .ToListAsync();

        return ProduitsExtrasFilter.VentesAvecLignesOfficielles(sales)
            .GroupBy(s => new
            {
                s.VendeurId,
                Nom = s.Vendeur != null ? s.Vendeur.Nom : "Non attribué",
                Couleur = s.Vendeur != null ? s.Vendeur.CouleurTicket : null
            })
            .Select(g =>
            {
                var lignesOff = g.SelectMany(s => ProduitsExtrasFilter.LignesOfficielles(s.Lines)).ToList();
                var ca = lignesOff.Sum(l => l.UnitPrice * l.Quantity);
                var count = g.Count();
                return new VendeurRapportViewModel
                {
                    VendeurId = g.Key.VendeurId,
                    NomVendeur = g.Key.Nom,
                    CouleurTicket = g.Key.Couleur,
                    NombreVentes = count,
                    ChiffreAffaires = ca,
                    NombreArticles = lignesOff.Sum(l => l.Quantity),
                    PanierMoyen = count > 0 ? ca / count : 0
                };
            })
            .OrderByDescending(v => v.ChiffreAffaires)
            .ToList();
    }

    private async Task<List<ReportMovementHistoryRowViewModel>> LoadStockMovementsHistoryRowsAsync()
    {
        var movements = await _db.StockMovements
            .AsNoTracking()
            .Include(m => m.Product!).ThenInclude(p => p.Category)
            .Include(m => m.Batch)
            .Where(m => m.Product == null
                        || m.Product.Category == null
                        || !m.Product.Category.EstHorsSysteme)
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

    [Authorize(Roles = AppRoles.FinancesAccess)]
    public async Task<IActionResult> Recapitulatif(DateTime? dateDebut = null, DateTime? dateFin = null)
    {
        var vm = await BuildRecapitulatifAsync(dateDebut, dateFin);
        return View(vm);
    }

    [Authorize(Roles = AppRoles.FinancesAccess)]
    public async Task<IActionResult> ImprimerRecapitulatif(DateTime? dateDebut = null, DateTime? dateFin = null)
    {
        var vm = await BuildRecapitulatifAsync(dateDebut, dateFin);
        return View("RecapitulatifPrint", vm);
    }

    [Authorize(Roles = AppRoles.FinancesAccess)]
    public async Task<IActionResult> ExportRecapitulatifCSV(DateTime? dateDebut = null, DateTime? dateFin = null)
    {
        var vm = await BuildRecapitulatifAsync(dateDebut, dateFin);
        var sb = ReportCsvFormatter.CreateBuilder();
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Récapitulatif"),
            ReportCsvFormatter.Escape($"{vm.DateDebut:yyyy-MM-dd} → {vm.DateFin:yyyy-MM-dd}")));
        sb.AppendLine();
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("Indicateur"), ReportCsvFormatter.Escape("Valeur")));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("CA Total (FCFA)"), ReportCsvFormatter.FcfaCsvAmount(vm.CATotal)));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("PA Total (FCFA)"), ReportCsvFormatter.FcfaCsvAmount(vm.PATotal)));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("Marge brute (FCFA)"), ReportCsvFormatter.FcfaCsvAmount(vm.MargeBrute)));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("Taux de marge (%)"), ReportCsvFormatter.DecimalInvariant(vm.TauxMarge)));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("TVA collectée (FCFA)"), ReportCsvFormatter.FcfaCsvAmount(vm.TVACollectee)));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("Panier moyen (FCFA)"), ReportCsvFormatter.FcfaCsvAmount(vm.PanierMoyen)));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("Nb ventes"), ReportCsvFormatter.IntInvariant(vm.NombreVentes)));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("Bons créés (FCFA)"), ReportCsvFormatter.FcfaCsvAmount(vm.TotalBons)));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("Bons réglés (FCFA)"), ReportCsvFormatter.FcfaCsvAmount(vm.TotalBonsRegle)));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("Bons en attente (FCFA)"), ReportCsvFormatter.FcfaCsvAmount(vm.TotalBonsEnAttente)));
        sb.AppendLine(ReportCsvFormatter.Join(ReportCsvFormatter.Escape("Avoirs (FCFA)"), ReportCsvFormatter.FcfaCsvAmount(vm.TotalAvoirs)));
        sb.AppendLine();
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Catégorie"),
            ReportCsvFormatter.Escape("CA"),
            ReportCsvFormatter.Escape("PA"),
            ReportCsvFormatter.Escape("Marge"),
            ReportCsvFormatter.Escape("% Marge"),
            ReportCsvFormatter.Escape("Articles")));
        foreach (var c in vm.CAParCategorie)
        {
            sb.AppendLine(ReportCsvFormatter.Join(
                ReportCsvFormatter.Escape(c.Categorie),
                ReportCsvFormatter.FcfaCsvAmount(c.CA),
                ReportCsvFormatter.FcfaCsvAmount(c.PA),
                ReportCsvFormatter.FcfaCsvAmount(c.Marge),
                ReportCsvFormatter.DecimalInvariant(c.TauxMarge),
                ReportCsvFormatter.IntInvariant(c.NbArticles)));
        }

        return ReportCsvFormatter.FileResult(this, sb.ToString(),
            $"recapitulatif_{vm.DateDebut:yyyyMMdd}_{vm.DateFin:yyyyMMdd}");
    }

    private async Task<RecapitulatifViewModel> BuildRecapitulatifAsync(DateTime? dateDebut, DateTime? dateFin)
    {
        var debut = (dateDebut ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1)).Date;
        var fin = (dateFin ?? DateTime.Today).Date;
        if (fin < debut)
            (debut, fin) = (fin, debut);

        var start = debut;
        var endExclusive = fin.AddDays(1);

        var ventes = await _db.Sales
            .AsNoTracking()
            .Include(s => s.Lines).ThenInclude(l => l.Product!).ThenInclude(p => p.Category)
            .Where(s => !s.IsAnnulee && !s.IsAdminTest && s.SoldAt >= start && s.SoldAt < endExclusive)
            .ToListAsync();

        var lignes = ventes.SelectMany(s => ProduitsExtrasFilter.LignesOfficielles(s.Lines)).ToList();
        var ventesOfficielles = ProduitsExtrasFilter.VentesAvecLignesOfficielles(ventes).ToList();
        var caTotal = lignes.Sum(l => l.UnitPrice * l.Quantity);
        var paTotal = lignes.Sum(l => (l.Product?.PurchasePrice ?? 0m) * l.Quantity);
        var marge = caTotal - paTotal;

        var totalBons = await _db.Bons
            .AsNoTracking()
            .Where(b => b.DateCreation >= start && b.DateCreation < endExclusive)
            .SumAsync(b => (decimal?)b.MontantTotal) ?? 0m;

        var totalBonsRegle = await _db.Bons
            .AsNoTracking()
            .Where(b => b.DateCreation >= start && b.DateCreation < endExclusive)
            .SumAsync(b => (decimal?)b.MontantRegle) ?? 0m;

        var totalAvoirs = await _db.Avoirs
            .AsNoTracking()
            .Where(a => a.DateCreation >= start && a.DateCreation < endExclusive)
            .SumAsync(a => (decimal?)a.MontantTotal) ?? 0m;

        return new RecapitulatifViewModel
        {
            DateDebut = debut,
            DateFin = fin,
            NombreVentes = ventesOfficielles.Count,
            CATotal = caTotal,
            PATotal = paTotal,
            MargeBrute = marge,
            TauxMarge = caTotal > 0 ? marge / caTotal * 100 : 0,
            TVACollectee = lignes.Sum(l =>
                TVACalculator.CalculerTVA(l.Product, l.UnitPrice, l.Quantity).MontantTVA),
            PanierMoyen = ventesOfficielles.Count > 0 ? caTotal / ventesOfficielles.Count : 0,
            TotalBons = totalBons,
            TotalBonsRegle = totalBonsRegle,
            TotalAvoirs = totalAvoirs,
            CAParCategorie = lignes
                .GroupBy(l => l.Product?.Category?.Name ?? "Sans catégorie")
                .Select(g => new RecapCategorieVm
                {
                    Categorie = g.Key,
                    CA = g.Sum(l => l.UnitPrice * l.Quantity),
                    PA = g.Sum(l => (l.Product?.PurchasePrice ?? 0m) * l.Quantity),
                    Marge = g.Sum(l => (l.UnitPrice - (l.Product?.PurchasePrice ?? 0m)) * l.Quantity),
                    NbArticles = g.Sum(l => l.Quantity)
                })
                .OrderByDescending(x => x.CA)
                .ToList()
        };
    }

    [Authorize(Roles = $"{AppRoles.PharmacienTitulaire},{AppRoles.Administrateur}")]
    public async Task<IActionResult> RapportExtras(DateTime? dateDebut = null, DateTime? dateFin = null)
    {
        var vm = await BuildRapportExtrasAsync(dateDebut, dateFin);
        return View(vm);
    }

    [Authorize(Roles = $"{AppRoles.PharmacienTitulaire},{AppRoles.Administrateur}")]
    public async Task<IActionResult> ImprimerRapportExtras(DateTime? dateDebut = null, DateTime? dateFin = null)
    {
        var vm = await BuildRapportExtrasAsync(dateDebut, dateFin);
        return View("RapportExtrasPrint", vm);
    }

    [Authorize(Roles = $"{AppRoles.PharmacienTitulaire},{AppRoles.Administrateur}")]
    public async Task<IActionResult> ExportRapportExtrasCSV(DateTime? dateDebut = null, DateTime? dateFin = null)
    {
        var vm = await BuildRapportExtrasAsync(dateDebut, dateFin);
        var sb = ReportCsvFormatter.CreateBuilder();
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Rapport Produits Extras"),
            ReportCsvFormatter.Escape($"{vm.DateDebut:yyyy-MM-dd} → {vm.DateFin:yyyy-MM-dd}")));
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Nombre de ventes"),
            ReportCsvFormatter.IntInvariant(vm.NombreVentes)));
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("CA Total Extras (FCFA)"),
            ReportCsvFormatter.FcfaCsvAmount(vm.CATotal)));
        sb.AppendLine();
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Date"),
            ReportCsvFormatter.Escape("N° vente"),
            ReportCsvFormatter.Escape("Produits Extras"),
            ReportCsvFormatter.Escape("CA Extras (FCFA)"),
            ReportCsvFormatter.Escape("Mode paiement"),
            ReportCsvFormatter.Escape("Vendeur")));

        foreach (var v in vm.Ventes)
        {
            var lignesExtras = ProduitsExtrasFilter.LignesExtras(v.Lines).ToList();
            var produits = string.Join(", ", lignesExtras.Select(l =>
                $"{l.Product?.CommercialName ?? "#"} ×{l.Quantity}"));
            var ca = lignesExtras.Sum(l => l.UnitPrice * l.Quantity);
            sb.AppendLine(ReportCsvFormatter.Join(
                ReportCsvFormatter.Escape(v.SoldAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                ReportCsvFormatter.IntInvariant(v.Id),
                ReportCsvFormatter.Escape(produits),
                ReportCsvFormatter.FcfaCsvAmount(ca),
                ReportCsvFormatter.Escape(PaymentMethodDisplay.GetName(v.PaymentMethod)),
                ReportCsvFormatter.Escape(v.Vendeur?.Nom ?? "Non attribué")));
        }

        return ReportCsvFormatter.FileResult(this, sb.ToString(),
            $"rapport-extras_{vm.DateDebut:yyyyMMdd}_{vm.DateFin:yyyyMMdd}");
    }

    private async Task<RapportExtrasViewModel> BuildRapportExtrasAsync(DateTime? dateDebut, DateTime? dateFin)
    {
        var debut = (dateDebut ?? DateTime.Today.AddDays(-30)).Date;
        var fin = (dateFin ?? DateTime.Today).Date;
        if (fin < debut)
            (debut, fin) = (fin, debut);

        var start = debut;
        var endExclusive = fin.AddDays(1);

        var ventesExtras = await ProduitsExtrasFilter.WhereAvecExtras(
                _db.Sales
                    .AsNoTracking()
                    .Include(s => s.Lines).ThenInclude(l => l.Product!).ThenInclude(p => p.Category)
                    .Include(s => s.Vendeur)
                    .Where(s => !s.IsAnnulee && !s.IsAdminTest && s.SoldAt >= start && s.SoldAt < endExclusive))
            .OrderByDescending(s => s.SoldAt)
            .ToListAsync();

        var caTotal = ventesExtras
            .SelectMany(s => ProduitsExtrasFilter.LignesExtras(s.Lines))
            .Sum(l => l.UnitPrice * l.Quantity);

        return new RapportExtrasViewModel
        {
            DateDebut = debut,
            DateFin = fin,
            NombreVentes = ventesExtras.Count,
            CATotal = caTotal,
            Ventes = ventesExtras
        };
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.PharmacienTitulaire},{AppRoles.Pharmacien},{AppRoles.Administrateur}")]
    public async Task<IActionResult> RapportEcarts(DateTime? date = null)
    {
        var targetDate = (date ?? DateTime.Today).Date;
        var nextDay = targetDate.AddDays(1);

        var produitsDetail = await _db.Products
            .AsNoTracking()
            .Include(p => p.ParentProduct)
            .Where(p => p.ParentProductId != null && p.IsActive)
            .OrderBy(p => p.CommercialName)
            .ToListAsync();

        var lignes = new List<EcartDetailViewModel>();

        foreach (var enfant in produitsDetail)
        {
            var parent = enfant.ParentProduct;
            if (parent == null)
                continue;

            var boitesOuvertes = await _db.StockMovements
                .AsNoTracking()
                .Where(m =>
                    m.ProductId == parent.Id
                    && m.Type == StockMovementType.Sortie
                    && m.Reason != null
                    && m.Reason.Contains("Ouverture boîte")
                    && m.OccurredAt >= targetDate
                    && m.OccurredAt < nextDay)
                .SumAsync(m => (int?)m.Quantity) ?? 0;

            var unitesVendues = await _db.SaleLines
                .AsNoTracking()
                .Where(l =>
                    l.ProductId == enfant.Id
                    && l.Sale != null
                    && l.Sale.SoldAt >= targetDate
                    && l.Sale.SoldAt < nextDay)
                .SumAsync(l => (int?)l.Quantity) ?? 0;

            var nbParBoite = enfant.NbUnitesParBoite ?? 0;
            var unitesTheorique = boitesOuvertes * nbParBoite;
            var ecart = unitesTheorique - unitesVendues;

            lignes.Add(new EcartDetailViewModel
            {
                ProduitBoite = parent.CommercialName,
                ProduitUnite = enfant.CommercialName,
                NbUnitesParBoite = nbParBoite,
                BoitesOuvertes = boitesOuvertes,
                UnitesTheorique = unitesTheorique,
                UnitesVendues = unitesVendues,
                StockUnitesActuel = enfant.StockQuantity,
                Ecart = ecart
            });
        }

        ViewBag.Date = targetDate;
        ViewBag.SuspectCount = lignes.Count(l => l.EstSuspect);
        return View(lignes);
    }
}
