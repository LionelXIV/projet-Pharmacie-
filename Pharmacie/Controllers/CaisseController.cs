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

[Authorize(Roles = AppRoles.CanAccessCaisse)]
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
        AppRoles.IsTitulaire(User) || User.IsInRole(AppRoles.Pharmacien);

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        var sessions = await _context.SessionCaisses
            .AsNoTracking()
            .Include(s => s.Ventes).ThenInclude(v => v.Sale).ThenInclude(sale => sale!.Lines)
            .Include(s => s.Depots)
            .Where(s => s.DateSession == today)
            .OrderBy(s => s.HeureOuverture)
            .ToListAsync();

        var sessionOuverte1 = sessions
            .FirstOrDefault(s => s.NumeroCaisse == 1 && s.Statut == SessionCaisseStatut.Ouverte);
        var sessionOuverte2 = sessions
            .FirstOrDefault(s => s.NumeroCaisse == 2 && s.Statut == SessionCaisseStatut.Ouverte);
        var derniereFermee1 = sessions
            .Where(s => s.NumeroCaisse == 1 && s.Statut == SessionCaisseStatut.Fermee)
            .OrderByDescending(s => s.HeureFermeture)
            .FirstOrDefault();
        var derniereFermee2 = sessions
            .Where(s => s.NumeroCaisse == 2 && s.Statut == SessionCaisseStatut.Fermee)
            .OrderByDescending(s => s.HeureFermeture)
            .FirstOrDefault();

        var userIds = sessions.Select(s => s.CaissierUserId).Distinct();
        var labels = await UserDisplayResolver.LoadLabelsByIdAsync(_context, userIds);

        ViewBag.Sessions = sessions;
        ViewBag.SessionOuverte1 = sessionOuverte1;
        ViewBag.SessionOuverte2 = sessionOuverte2;
        ViewBag.DerniereFermee1 = derniereFermee1;
        ViewBag.DerniereFermee2 = derniereFermee2;
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
                && s.Statut == SessionCaisseStatut.Ouverte);

        if (deja != null && !IsAdminOrPharmacien)
        {
            var labels = await UserDisplayResolver.LoadLabelsByIdAsync(_context, new[] { deja.CaissierUserId });
            var nomCaissier = UserDisplayResolver.Resolve(labels, deja.CaissierUserId);
            TempData["Error"] =
                $"La {deja.NomCaisse} est déjà ouverte par {nomCaissier} depuis {deja.HeureOuverture:HH:mm}. " +
                "Demandez au Pharmacien Titulaire de forcer la fermeture si nécessaire.";
            return RedirectToAction(nameof(Index));
        }

        ViewBag.NumeroCaisse = numero;
        ViewBag.IsReouverture = await _context.SessionCaisses.AsNoTracking()
            .AnyAsync(s =>
                s.NumeroCaisse == numero
                && s.DateSession == DateTime.Today
                && s.Statut == SessionCaisseStatut.Fermee);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ouvrir(int NumeroCaisse, decimal FondDepart)
    {
        var (ok, error, _) = await _caisseService.OuvrirCaisseAsync(
            NumeroCaisse, FondDepart, CurrentUserId, User.IsInRole(AppRoles.Administrateur));
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
        var caEspeces = sales.Where(s => s.PaymentMethod == PaymentMethod.Especes).Sum(CaisseService.CalculerTotalSale);
        ViewBag.NbVentes = sales.Count;
        ViewBag.CaEspeces = caEspeces;
        ViewBag.CaWave = sales.Where(s => s.PaymentMethod == PaymentMethod.Wave).Sum(CaisseService.CalculerTotalSale);
        ViewBag.CaOM = sales.Where(s => s.PaymentMethod == PaymentMethod.OrangeMoney).Sum(CaisseService.CalculerTotalSale);
        ViewBag.CaAutre = sales
            .Where(s => s.PaymentMethod is not (PaymentMethod.Especes or PaymentMethod.Wave or PaymentMethod.OrangeMoney))
            .Sum(CaisseService.CalculerTotalSale);

        var totalDepots = await _context.DepotCaisses
            .Where(d => d.SessionCaisseId == id)
            .SumAsync(d => (decimal?)d.MontantDepose) ?? 0;
        ViewBag.TotalDepots = totalDepots;
        ViewBag.AttenduEspeces = session.FondDepart + caEspeces - totalDepots;

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
            .ThenInclude(l => l.Product!).ThenInclude(p => p.Category)
            .Include(s => s.Depots)
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

        var depots = session.Depots.OrderBy(d => d.HeureDepot).ToList();
        var totalDepots = depots.Sum(d => d.MontantDepose);

        var billetage = string.IsNullOrEmpty(session.BilletageJson)
            ? null
            : JsonSerializer.Deserialize<BilletageDetail>(session.BilletageJson);

        var theoriqueEspeces = session.FondDepart + caEspeces - totalDepots;
        var billetageTotal = session.BilletageTotal ?? billetage?.Total ?? 0;
        var ecart = billetageTotal - theoriqueEspeces;

        var labels = await UserDisplayResolver.LoadLabelsByIdAsync(_context, new[] { session.CaissierUserId });

        var toutesLignes = sales.SelectMany(s => s.Lines).ToList();
        var (qte1, qte2, prime1, prime2, primeTotale) =
            PrimeRisqueCalculator.CalculerPrimes(toutesLignes);

        ViewBag.CaissierNom = UserDisplayResolver.Resolve(labels, session.CaissierUserId);
        ViewBag.Sales = sales;
        ViewBag.Depots = depots;
        ViewBag.TotalDepots = totalDepots;
        ViewBag.CaEspeces = caEspeces;
        ViewBag.CaWave = caWave;
        ViewBag.CaOM = caOM;
        ViewBag.CaAutre = caAutre;
        ViewBag.BonsTotal = bons;
        ViewBag.TheoriqueEspeces = theoriqueEspeces;
        ViewBag.TotalCa = caEspeces + caWave + caOM + caAutre;
        ViewBag.Billetage = billetage;
        ViewBag.Ecart = ecart;
        ViewBag.QteTableau1 = qte1;
        ViewBag.QteTableau2 = qte2;
        ViewBag.PrimeTableau1 = prime1;
        ViewBag.PrimeTableau2 = prime2;
        ViewBag.PrimeTotale = primeTotale;

        return View(session);
    }

    [HttpGet]
    public async Task<IActionResult> Depot(int sessionId, DepotCaisseType type = DepotCaisseType.Normal)
    {
        var session = await _context.SessionCaisses
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null) return NotFound();

        if (session.Statut != SessionCaisseStatut.Ouverte)
        {
            TempData["Error"] = "Impossible de déposer : la caisse est fermée.";
            return RedirectToAction(nameof(Index));
        }

        if (!IsAdminOrPharmacien && session.CaissierUserId != CurrentUserId)
        {
            TempData["Error"] = "Vous ne pouvez déposer que depuis votre propre caisse.";
            return RedirectToAction(nameof(Index));
        }

        var solde = await _caisseService.GetSoldeTheoriqueAsync(sessionId);
        ViewBag.Session = session;
        ViewBag.SoldeTheorique = solde;
        ViewBag.Type = type;
        ViewBag.SeuilAtteint = solde >= CaisseService.SeuilDepotFcfa;
        ViewBag.Seuil = CaisseService.SeuilDepotFcfa;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Depot(int sessionId, decimal montant, DepotCaisseType type)
    {
        var (success, error, depotId) = await _caisseService.EnregistrerDepotAsync(
            sessionId, montant, type, CurrentUserId, IsAdminOrPharmacien);

        if (!success)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Depot), new { sessionId, type });
        }

        TempData["Success"] = "Dépôt enregistré avec succès.";
        return RedirectToAction(nameof(TicketDepot), new { id = depotId });
    }

    [HttpGet]
    public async Task<IActionResult> TicketDepot(int id)
    {
        var depot = await _context.DepotCaisses
            .AsNoTracking()
            .Include(d => d.SessionCaisse)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (depot == null) return NotFound();

        if (!IsAdminOrPharmacien && depot.EffectueParUserId != CurrentUserId
            && depot.SessionCaisse.CaissierUserId != CurrentUserId)
        {
            TempData["Error"] = "Accès refusé à ce ticket.";
            return RedirectToAction(nameof(Index));
        }

        var labels = await UserDisplayResolver.LoadLabelsByIdAsync(
            _context, new[] { depot.EffectueParUserId, depot.SessionCaisse.CaissierUserId });
        ViewBag.CaissierNom = UserDisplayResolver.Resolve(labels, depot.EffectueParUserId);

        return View(depot);
    }

    [HttpGet]
    [Authorize(Roles = $"{AppRoles.PharmacienTitulaire},{AppRoles.Administrateur},{AppRoles.Pharmacien}")]
    public async Task<IActionResult> RapportConsolide(DateTime? date)
    {
        var d = (date ?? DateTime.Today).Date;
        var sessions = await _context.SessionCaisses
            .AsNoTracking()
            .Include(s => s.Ventes).ThenInclude(v => v.Sale).ThenInclude(sale => sale!.Lines)
            .ThenInclude(l => l.Product!).ThenInclude(p => p.Category)
            .Include(s => s.Depots)
            .Where(s => s.DateSession == d)
            .OrderBy(s => s.NumeroCaisse)
            .ThenBy(s => s.HeureOuverture)
            .ToListAsync();

        var labels = await UserDisplayResolver.LoadLabelsByIdAsync(
            _context, sessions.Select(s => s.CaissierUserId));

        var toutesLignesJour = sessions
            .SelectMany(s => s.Ventes)
            .Select(v => v.Sale)
            .Where(s => s != null)
            .Cast<Sale>()
            .SelectMany(s => s.Lines)
            .ToList();

        var (qte1J, qte2J, prime1J, prime2J, primeTotaleJ) =
            PrimeRisqueCalculator.CalculerPrimes(toutesLignesJour);

        ViewBag.Date = d;
        ViewBag.Sessions = sessions;
        ViewBag.SessionsCaisse1 = sessions.Where(s => s.NumeroCaisse == 1).ToList();
        ViewBag.SessionsCaisse2 = sessions.Where(s => s.NumeroCaisse == 2).ToList();
        ViewBag.Labels = labels;
        ViewBag.ConsoQteTableau1 = qte1J;
        ViewBag.ConsoQteTableau2 = qte2J;
        ViewBag.ConsoPrimeTableau1 = prime1J;
        ViewBag.ConsoPrimeTableau2 = prime2J;
        ViewBag.ConsoPrimeTotale = primeTotaleJ;

        return View();
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> VerifierCaisseOuverte()
    {
        if (User.IsInRole(AppRoles.Administrateur))
            return Json(new { ouverte = false });

        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId))
            return Json(new { ouverte = false });

        var session = await _context.SessionCaisses
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.CaissierUserId == userId
                && s.Statut == SessionCaisseStatut.Ouverte);

        if (session == null)
            return Json(new { ouverte = false });

        return Json(new
        {
            ouverte = true,
            sessionId = session.Id,
            nomCaisse = session.NomCaisse,
            heureOuverture = session.HeureOuverture.ToString("HH:mm"),
            fermerUrl = Url.Action(nameof(Fermer), "Caisse", new { id = session.Id })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.PharmacienTitulaire},{AppRoles.Administrateur}")]
    public async Task<IActionResult> ForcerFermeture(int sessionId)
    {
        if (!AppRoles.IsTitulaire(User))
        {
            TempData["Error"] = "Action réservée au Pharmacien Titulaire.";
            return RedirectToAction(nameof(Index));
        }

        var session = await _context.SessionCaisses
            .FirstOrDefaultAsync(s =>
                s.Id == sessionId
                && s.Statut == SessionCaisseStatut.Ouverte);

        if (session == null)
        {
            TempData["Error"] = "Session introuvable ou déjà fermée.";
            return RedirectToAction(nameof(Index));
        }

        session.HeureFermeture = DateTime.Now;
        session.Statut = SessionCaisseStatut.Fermee;
        session.Notes =
            $"Fermée de force par le Pharmacien Titulaire le {DateTime.Now:dd/MM/yyyy HH:mm}";

        await _context.SaveChangesAsync();

        TempData["Success"] = $"Session {session.NomCaisse} fermée de force.";
        return RedirectToAction(nameof(Index));
    }
}
