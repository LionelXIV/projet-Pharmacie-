using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models.ViewModels;
using Pharmacie.Reporting;

namespace Pharmacie.Controllers;

[Authorize(Roles =
    AppRoles.PharmacienTitulaire + "," +
    AppRoles.Pharmacien + "," +
    AppRoles.Administrateur)]
public class StatistiquesController : Controller
{
    private readonly ApplicationDbContext _context;

    public StatistiquesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        DateTime? debut1 = null,
        DateTime? fin1 = null,
        DateTime? debut2 = null,
        DateTime? fin2 = null,
        string periode = "semaine")
    {
        var (d1, f1) = ResolvePeriode1(debut1, fin1, periode);
        var d2 = debut2;
        var f2 = fin2;

        var topVentes1 = await GetTopVentesAsync(d1, f1);

        List<TopVenteItem>? topVentes2 = null;
        if (d2.HasValue && f2.HasValue)
            topVentes2 = await GetTopVentesAsync(d2.Value, f2.Value);

        var ventesParJour = await GetVentesParJourAsync(d1, f1);

        ViewBag.TopVentes1 = topVentes1;
        ViewBag.TopVentes2 = topVentes2;
        ViewBag.VentesParJour = ventesParJour;
        ViewBag.VentesParJourJson = JsonSerializer.Serialize(
            ventesParJour.Select(v => new
            {
                jour = v.Jour.ToString("yyyy-MM-dd"),
                nbVentes = v.NbVentes,
                cA = v.CA
            }));
        ViewBag.D1 = d1.ToString("yyyy-MM-dd");
        ViewBag.F1 = f1.ToString("yyyy-MM-dd");
        ViewBag.D2 = d2?.ToString("yyyy-MM-dd");
        ViewBag.F2 = f2?.ToString("yyyy-MM-dd");
        ViewBag.TotalCA1 = topVentes1.Sum(x => x.CA);
        ViewBag.TotalMarge1 = topVentes1.Sum(x => x.Marge);
        ViewBag.TotalQte1 = topVentes1.Sum(x => x.QuantiteVendue);

        return View(topVentes1);
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(
        DateTime? debut = null,
        DateTime? fin = null)
    {
        var d = (debut ?? DateTime.Today.AddDays(-7)).Date;
        var f = (fin ?? DateTime.Today).Date;
        var top = await GetTopVentesAsync(d, f);

        var sb = ReportCsvFormatter.CreateBuilder();
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Produit"),
            ReportCsvFormatter.Escape("Qté vendue"),
            ReportCsvFormatter.Escape("Prix cession"),
            ReportCsvFormatter.Escape("Prix vente moy."),
            ReportCsvFormatter.Escape("CA"),
            ReportCsvFormatter.Escape("Marge"),
            ReportCsvFormatter.Escape("Taux marge %")));

        foreach (var item in top)
        {
            sb.AppendLine(ReportCsvFormatter.Join(
                ReportCsvFormatter.Escape(item.Nom),
                ReportCsvFormatter.IntInvariant(item.QuantiteVendue),
                ReportCsvFormatter.FcfaCsvAmount(item.PrixCession),
                ReportCsvFormatter.FcfaCsvAmount(item.PrixVenteMoyen),
                ReportCsvFormatter.FcfaCsvAmount(item.CA),
                ReportCsvFormatter.FcfaCsvAmount(item.Marge),
                ReportCsvFormatter.DecimalInvariant(item.TauxMarge)));
        }

        return ReportCsvFormatter.FileResult(this, sb.ToString(), "top-ventes");
    }

    private static (DateTime Debut, DateTime Fin) ResolvePeriode1(
        DateTime? debut1,
        DateTime? fin1,
        string periode)
    {
        if (debut1.HasValue || fin1.HasValue)
        {
            var d = (debut1 ?? DateTime.Today.AddDays(-7)).Date;
            var f = (fin1 ?? DateTime.Today).Date;
            if (d > f)
                (d, f) = (f, d);
            return (d, f);
        }

        var today = DateTime.Today;
        return (periode ?? "semaine").Trim().ToLowerInvariant() switch
        {
            "aujourd hui" or "aujourdhui" or "jour" => (today, today),
            "quinzaine" => (today.AddDays(-15), today),
            "mois" => (today.AddDays(-30), today),
            _ => (today.AddDays(-7), today)
        };
    }

    private async Task<List<TopVenteItem>> GetTopVentesAsync(DateTime debut, DateTime fin)
    {
        var start = debut.Date;
        var endExclusive = fin.Date.AddDays(1);

        var lines = await _context.SaleLines
            .AsNoTracking()
            .Where(sl => sl.Sale != null
                && sl.Sale.SoldAt >= start
                && sl.Sale.SoldAt < endExclusive
                && !sl.Sale.IsAnnulee
                && !sl.Sale.IsAdminTest
                && sl.Product != null)
            .Select(sl => new
            {
                sl.ProductId,
                Nom = sl.Product!.CommercialName,
                PrixCession = sl.Product.PurchasePrice,
                sl.UnitPrice,
                sl.Quantity
            })
            .ToListAsync();

        return lines
            .GroupBy(x => new { x.ProductId, x.Nom, x.PrixCession })
            .Select(g =>
            {
                var qte = g.Sum(x => x.Quantity);
                var ca = g.Sum(x => x.UnitPrice * x.Quantity);
                return new TopVenteItem
                {
                    ProductId = g.Key.ProductId,
                    Nom = g.Key.Nom,
                    PrixCession = g.Key.PrixCession,
                    PrixVenteMoyen = g.Average(x => x.UnitPrice),
                    QuantiteVendue = qte,
                    CA = ca,
                    Marge = ca - (g.Key.PrixCession * qte)
                };
            })
            .OrderByDescending(x => x.CA)
            .Take(50)
            .ToList();
    }

    private async Task<List<VenteParJourItem>> GetVentesParJourAsync(DateTime debut, DateTime fin)
    {
        var start = debut.Date;
        var endExclusive = fin.Date.AddDays(1);

        var sales = await _context.Sales
            .AsNoTracking()
            .Where(s => s.SoldAt >= start
                && s.SoldAt < endExclusive
                && !s.IsAnnulee
                && !s.IsAdminTest)
            .Select(s => new
            {
                s.SoldAt,
                CA = s.Lines.Sum(l => l.UnitPrice * l.Quantity)
            })
            .ToListAsync();

        return sales
            .GroupBy(x => x.SoldAt.Date)
            .Select(g => new VenteParJourItem
            {
                Jour = g.Key,
                NbVentes = g.Count(),
                CA = g.Sum(x => x.CA)
            })
            .OrderBy(x => x.Jour)
            .ToList();
    }
}
