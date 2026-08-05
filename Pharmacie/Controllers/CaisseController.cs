using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Services;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.Sales)]
public class CaisseController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly CaisseService _caisseService;

    public CaisseController(ApplicationDbContext context, CaisseService caisseService)
    {
        _context = context;
        _caisseService = caisseService;
    }

    private bool IsAdminOrPharmacien =>
        User.IsInRole(AppRoles.Administrateur) || User.IsInRole(AppRoles.Pharmacien);

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        var sessions = await _context.SessionCaisses
            .AsNoTracking()
            .Include(s => s.Ventes).ThenInclude(v => v.Sale).ThenInclude(sale => sale!.Lines)
            .Where(s => s.DateSession == today)
            .ToListAsync();

        var caisse1 = sessions
            .Where(s => s.NumeroCaisse == 1)
            .OrderByDescending(s => s.Id)
            .FirstOrDefault();
        var caisse2 = sessions
            .Where(s => s.NumeroCaisse == 2)
            .OrderByDescending(s => s.Id)
            .FirstOrDefault();

        var userIds = sessions.Select(s => s.CaissierUserId).Distinct();
        var labels = await UserDisplayResolver.LoadLabelsByIdAsync(_context, userIds);

        ViewBag.Caisse1 = caisse1;
        ViewBag.Caisse2 = caisse2;
        ViewBag.Labels = labels;
        ViewBag.IsAdminOrPharmacien = IsAdminOrPharmacien;
        ViewBag.CurrentUserId = CurrentUserId;
        ViewBag.MaSession = sessions.FirstOrDefault(s =>
            s.CaissierUserId == CurrentUserId && s.Statut == SessionCaisseStatut.Ouverte);

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Ouvrir(int numero)
    {
        if (numero is not (1 or 2))
            return RedirectToAction(nameof(Index));

        var existing = await _caisseService.GetSessionOuverteAsync(CurrentUserId);
        if (existing != null)
        {
            TempData["Error"] = $"Vous avez déjà {existing.NomCaisse} ouverte.";
            return RedirectToAction(nameof(Index));
        }

        var deja = await _context.SessionCaisses.AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.NumeroCaisse == numero
                && s.DateSession == DateTime.Today
                && s.Statut == SessionCaisseStatut.Ouverte);

        if (deja != null && !IsAdminOrPharmacien)
        {
            TempData["Error"] = $"La {(numero == 1 ? "Caisse Matin" : "Caisse Soir")} est déjà ouverte.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.NumeroCaisse = numero;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ouvrir(int NumeroCaisse, decimal FondDepart)
    {
        var (ok, error, _) = await _caisseService.OuvrirCaisseAsync(NumeroCaisse, FondDepart, CurrentUserId);
        if (!ok)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Ouvrir), new { numero = NumeroCaisse });
        }

        TempData["Success"] = $"Caisse {(NumeroCaisse == 1 ? "Matin" : "Soir")} ouverte avec succès.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Fermer(int id)
    {
        var session = await _context.SessionCaisses
            .AsNoTracking()
            .Include(s => s.Ventes).ThenInclude(v => v.Sale).ThenInclude(sale => sale!.Lines)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session == null) return NotFound();
        if (session.Statut == SessionCaisseStatut.Fermee)
        {
            TempData["Error"] = "Cette caisse est déjà fermée.";
            return RedirectToAction(nameof(Rapport), new { id });
        }

        if (!IsAdminOrPharmacien && session.CaissierUserId != CurrentUserId)
        {
            TempData["Error"] = "Vous ne pouvez fermer que votre propre caisse.";
            return RedirectToAction(nameof(Index));
        }

        var sales = session.Ventes.Select(v => v.Sale).Where(s => s != null).Cast<Sale>().ToList();
        ViewBag.NbVentes = sales.Count;
        ViewBag.CaEspeces = sales.Where(s => s.PaymentMethod == PaymentMethod.Especes).Sum(CaisseService.CalculerTotalSale);
        ViewBag.CaWave = sales.Where(s => s.PaymentMethod == PaymentMethod.Wave).Sum(CaisseService.CalculerTotalSale);
        ViewBag.CaOM = sales.Where(s => s.PaymentMethod == PaymentMethod.OrangeMoney).Sum(CaisseService.CalculerTotalSale);
        ViewBag.CaAutre = sales
            .Where(s => s.PaymentMethod is not (PaymentMethod.Especes or PaymentMethod.Wave or PaymentMethod.OrangeMoney))
            .Sum(CaisseService.CalculerTotalSale);
        ViewBag.AttenduEspeces = session.FondDepart + (decimal)ViewBag.CaEspeces;

        return View(session);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Fermer(int SessionId, BilletageDetail billetage, string? Notes)
    {
        billetage ??= new BilletageDetail();
        var (ok, error) = await _caisseService.FermerCaisseAsync(
            SessionId, billetage, Notes, CurrentUserId, IsAdminOrPharmacien);

        if (!ok)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Fermer), new { id = SessionId });
        }

        TempData["Success"] = "Caisse fermée avec succès.";
        return RedirectToAction(nameof(Rapport), new { id = SessionId });
    }

    [HttpGet]
    public async Task<IActionResult> Rapport(int id)
    {
        var session = await _context.SessionCaisses
            .AsNoTracking()
            .Include(s => s.Ventes).ThenInclude(v => v.Sale).ThenInclude(sale => sale!.Lines)
            .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session == null) return NotFound();

        if (!IsAdminOrPharmacien && session.CaissierUserId != CurrentUserId)
        {
            TempData["Error"] = "Accès refusé à ce rapport.";
            return RedirectToAction(nameof(Index));
        }

        var sales = session.Ventes.Select(v => v.Sale).Where(s => s != null).Cast<Sale>()
            .OrderBy(s => s.SoldAt).ToList();

        var caEspeces = sales.Where(s => s.PaymentMethod == PaymentMethod.Especes).Sum(CaisseService.CalculerTotalSale);
        var caWave = sales.Where(s => s.PaymentMethod == PaymentMethod.Wave).Sum(CaisseService.CalculerTotalSale);
        var caOM = sales.Where(s => s.PaymentMethod == PaymentMethod.OrangeMoney).Sum(CaisseService.CalculerTotalSale);
        var caAutre = sales
            .Where(s => s.PaymentMethod is not (PaymentMethod.Especes or PaymentMethod.Wave or PaymentMethod.OrangeMoney))
            .Sum(CaisseService.CalculerTotalSale);

        var bons = await _context.Bons.AsNoTracking()
            .Where(b => b.CreatedByUserId == session.CaissierUserId
                        && b.DateCreation.Date == session.DateSession
                        && b.Statut != BonStatut.Annule)
            .SumAsync(b => (decimal?)b.MontantTotal) ?? 0;

        var billetage = string.IsNullOrEmpty(session.BilletageJson)
            ? null
            : JsonSerializer.Deserialize<BilletageDetail>(session.BilletageJson);

        var theoriqueEspeces = session.FondDepart + caEspeces;
        var billetageTotal = session.BilletageTotal ?? billetage?.Total ?? 0;
        var ecart = billetageTotal - theoriqueEspeces;

        var labels = await UserDisplayResolver.LoadLabelsByIdAsync(_context, new[] { session.CaissierUserId });

        ViewBag.CaissierNom = UserDisplayResolver.Resolve(labels, session.CaissierUserId);
        ViewBag.Sales = sales;
        ViewBag.CaEspeces = caEspeces;
        ViewBag.CaWave = caWave;
        ViewBag.CaOM = caOM;
        ViewBag.CaAutre = caAutre;
        ViewBag.BonsTotal = bons;
        ViewBag.TheoriqueEspeces = theoriqueEspeces;
        ViewBag.TotalCa = caEspeces + caWave + caOM + caAutre;
        ViewBag.Billetage = billetage;
        ViewBag.Ecart = ecart;

        return View(session);
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.Administrateur},{AppRoles.Pharmacien}")]
    public async Task<IActionResult> RapportConsolide(DateTime? date)
    {
        var d = (date ?? DateTime.Today).Date;
        var sessions = await _context.SessionCaisses
            .AsNoTracking()
            .Include(s => s.Ventes).ThenInclude(v => v.Sale).ThenInclude(sale => sale!.Lines)
            .Where(s => s.DateSession == d)
            .OrderBy(s => s.NumeroCaisse)
            .ThenByDescending(s => s.Id)
            .ToListAsync();

        // Une session par caisse (la plus récente)
        var c1 = sessions.Where(s => s.NumeroCaisse == 1).FirstOrDefault();
        var c2 = sessions.Where(s => s.NumeroCaisse == 2).FirstOrDefault();

        var labels = await UserDisplayResolver.LoadLabelsByIdAsync(
            _context, sessions.Select(s => s.CaissierUserId));

        ViewBag.Date = d;
        ViewBag.Caisse1 = c1;
        ViewBag.Caisse2 = c2;
        ViewBag.Labels = labels;

        return View();
    }
}
