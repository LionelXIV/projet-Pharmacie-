using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Services;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.Sales)]
public class AvoirsController : Controller
{
    private const int PageSize = 50;

    private readonly ApplicationDbContext _context;
    private readonly AvoirService _avoirService;
    private readonly ILogger<AvoirsController> _logger;

    public AvoirsController(
        ApplicationDbContext context,
        AvoirService avoirService,
        ILogger<AvoirsController> logger)
    {
        _context = context;
        _avoirService = avoirService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? statut, string? q, DateTime? from, DateTime? to, int page = 1)
    {
        if (page < 1) page = 1;

        var query = _context.Avoirs
            .AsNoTracking()
            .Include(a => a.Vendeur)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(statut) && Enum.TryParse<AvoirStatut>(statut, out var s))
            query = query.Where(a => a.Statut == s);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(a => a.ClientNom.Contains(term) || a.Numero.Contains(term));
        }

        if (from.HasValue)
            query = query.Where(a => a.DateCreation >= from.Value.Date);
        if (to.HasValue)
            query = query.Where(a => a.DateCreation < to.Value.Date.AddDays(1));

        var total = await query.CountAsync();
        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)PageSize);
        if (page > totalPages) page = totalPages;

        var avoirs = await query
            .OrderByDescending(a => a.DateCreation)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = total;
        ViewBag.FilterStatut = statut;
        ViewBag.FilterQ = q;
        ViewBag.FilterFrom = from?.ToString("yyyy-MM-dd");
        ViewBag.FilterTo = to?.ToString("yyyy-MM-dd");

        return View(avoirs);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateVendeursAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string clientNom,
        string? clientTelephone,
        string? numeroIdentite,
        string? notes,
        int? vendeurId,
        PaymentMethod paymentMethod,
        List<int>? productIds,
        List<int>? quantities)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(clientNom))
                ModelState.AddModelError(nameof(clientNom), "Le nom du client est obligatoire.");

            productIds ??= new List<int>();
            quantities ??= new List<int>();

            if (!productIds.Any(id => id > 0))
                ModelState.AddModelError(string.Empty, "Ajoutez au moins un produit.");

            if (paymentMethod is not (PaymentMethod.Especes or PaymentMethod.Wave or PaymentMethod.OrangeMoney))
                ModelState.AddModelError(nameof(paymentMethod), "Mode de paiement non autorisé pour un avoir.");

            if (!ModelState.IsValid)
            {
                await PopulateVendeursAsync(vendeurId);
                return View();
            }

            var lignes = new List<(int, int)>();
            for (var i = 0; i < productIds.Count; i++)
            {
                if (productIds[i] <= 0) continue;
                var qty = i < quantities.Count ? quantities[i] : 0;
                if (qty <= 0) continue;
                lignes.Add((productIds[i], qty));
            }

            if (!lignes.Any())
            {
                ModelState.AddModelError(string.Empty, "Ajoutez au moins un produit avec une quantité.");
                await PopulateVendeursAsync(vendeurId);
                return View();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var (success, error, avoirId) = await _avoirService.CreateAvoirAsync(
                clientNom, clientTelephone, numeroIdentite, lignes,
                paymentMethod, userId, vendeurId, notes);

            if (success)
            {
                TempData["NewAvoir"] = true;
                TempData["Success"] = "Avoir créé avec succès.";
                return RedirectToAction(nameof(Details), new { id = avoirId });
            }

            ModelState.AddModelError(string.Empty, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur création avoir");
            ModelState.AddModelError(string.Empty, $"Erreur inattendue : {ex.Message}");
        }

        await PopulateVendeursAsync(vendeurId);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var avoir = await _context.Avoirs
            .AsNoTracking()
            .Include(a => a.Lignes).ThenInclude(l => l.Product)
            .Include(a => a.Vendeur)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (avoir == null) return NotFound();
        return View(avoir);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarquerLivre(int id, int ligneId)
    {
        var (success, error) = await _avoirService.MarquerLivreAsync(id, ligneId);
        TempData[success ? "Success" : "Error"] = success ? "Ligne marquée comme livrée." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.Administrateur},{AppRoles.Pharmacien}")]
    public async Task<IActionResult> AnnulerAvoir(int id)
    {
        var (success, error) = await _avoirService.AnnulerAvoirAsync(id);
        TempData[success ? "Success" : "Error"] = success ? "Avoir annulé." : error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Ticket(int id)
    {
        var avoir = await _context.Avoirs
            .AsNoTracking()
            .Include(a => a.Lignes).ThenInclude(l => l.Product)
            .Include(a => a.Vendeur)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (avoir == null) return NotFound();
        return View(avoir);
    }

    private async Task PopulateVendeursAsync(int? selectedId = null)
    {
        ViewBag.Vendeurs = await _context.Vendeurs
            .AsNoTracking()
            .Where(v => v.IsActif)
            .OrderBy(v => v.Nom)
            .Select(v => new { v.Id, v.Nom })
            .ToListAsync();
        ViewBag.SelectedVendeurId = selectedId;
    }
}
