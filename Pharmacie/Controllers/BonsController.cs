using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Reporting;
using Pharmacie.Services;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.CanCreateBon)]
public class BonsController : Controller
{
    private const int PageSize = 50;

    private readonly ApplicationDbContext _context;
    private readonly BonService _bonService;
    private readonly CaisseService _caisseService;
    private readonly ILogger<BonsController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;

    public BonsController(
        ApplicationDbContext context,
        BonService bonService,
        CaisseService caisseService,
        ILogger<BonsController> logger,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _bonService = bonService;
        _caisseService = caisseService;
        _logger = logger;
        _userManager = userManager;
    }

    // ─── Liste ───────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index(string? statut, string? client, string? numero,
        DateTime? from, DateTime? to, int page = 1)
    {
        if (page < 1) page = 1;

        var q = _context.Bons
            .AsNoTracking()
            .Include(b => b.Vendeur)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(statut) && Enum.TryParse<BonStatut>(statut, out var s))
            q = q.Where(b => b.Statut == s);
        if (!string.IsNullOrWhiteSpace(client))
            q = q.Where(b => b.ClientNom.Contains(client));
        if (!string.IsNullOrWhiteSpace(numero))
            q = q.Where(b => b.Numero.Contains(numero));
        if (from.HasValue)
            q = q.Where(b => b.DateCreation >= from.Value.Date);
        if (to.HasValue)
            q = q.Where(b => b.DateCreation < to.Value.Date.AddDays(1));

        var total = await q.CountAsync();
        var totalPages = total == 0 ? 1 : (int)Math.Ceiling(total / (double)PageSize);
        if (page > totalPages) page = totalPages;

        var bons = await q
            .OrderByDescending(b => b.DateCreation)
            .ThenByDescending(b => b.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = total;
        ViewBag.FilterStatut = statut;
        ViewBag.FilterClient = client;
        ViewBag.FilterNumero = numero;
        ViewBag.FilterFrom = from?.ToString("yyyy-MM-dd");
        ViewBag.FilterTo = to?.ToString("yyyy-MM-dd");

        return View(bons);
    }

    // ─── Création ────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var isAdmin = User.IsInRole(AppRoles.Administrateur);
        if (!isAdmin)
        {
            var sessionOuverte = await _caisseService.GetSessionOuverteAsync(userId);
            if (sessionOuverte == null)
            {
                TempData["Warning"] = "Ouvrez une caisse avant de créer un bon.";
                return RedirectToAction("Index", "Caisse");
            }
        }

        await PopulateVendeursAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        string clientNom, string? clientTelephone, string? notes, int? vendeurId,
        string? numeroIdentite,
        List<int>? productIds, List<int>? quantities,
        List<decimal>? discountPercents, List<decimal>? discountAmounts, List<string>? discountTypes)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(clientNom))
            {
                ModelState.AddModelError(nameof(clientNom), "Le nom du client est obligatoire.");
            }

            productIds ??= new List<int>();
            quantities ??= new List<int>();
            discountPercents ??= new List<decimal>();
            discountAmounts ??= new List<decimal>();
            discountTypes ??= new List<string>();

            if (!productIds.Any(id => id > 0))
            {
                ModelState.AddModelError(string.Empty, "Ajoutez au moins un produit.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateVendeursAsync(vendeurId);
                return View();
            }

            var lignes = new List<(int, int, decimal, decimal, string)>();
            for (var i = 0; i < productIds.Count; i++)
            {
                if (productIds[i] <= 0) continue;
                if (i < quantities.Count && quantities[i] <= 0) continue;
                var qty = i < quantities.Count ? quantities[i] : 1;
                var dp = i < discountPercents.Count ? discountPercents[i] : 0m;
                var da = i < discountAmounts.Count ? discountAmounts[i] : 0m;
                var dt = i < discountTypes.Count ? discountTypes[i] : "";
                lignes.Add((productIds[i], qty, dp, da, dt));
            }

            if (!lignes.Any())
            {
                ModelState.AddModelError(string.Empty, "Ajoutez au moins un produit avec une quantité.");
                await PopulateVendeursAsync(vendeurId);
                return View();
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var isAdmin = User.IsInRole(AppRoles.Administrateur);
            if (!isAdmin)
            {
                var sessionOuverte = await _caisseService.GetSessionOuverteAsync(userId);
                if (sessionOuverte == null)
                {
                    TempData["Warning"] = "Ouvrez une caisse avant de créer un bon.";
                    return RedirectToAction("Index", "Caisse");
                }
            }

            var (success, error, bonId) = await _bonService.CreateBonAsync(
                clientNom, clientTelephone, notes, lignes, userId, vendeurId, numeroIdentite);

            if (success)
            {
                TempData["NewBon"] = true;
                return RedirectToAction(nameof(Details), new { id = bonId });
            }

            ModelState.AddModelError(string.Empty, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur création bon");
            ModelState.AddModelError(string.Empty, $"Erreur inattendue : {ex.Message}");
        }

        await PopulateVendeursAsync(vendeurId);
        return View();
    }

    /// <summary>
    /// Création d'un bon depuis le POS vente (mode Crédit / Bon).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFromVente(BonCreateFromVenteViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.ClientNom))
        {
            TempData["Error"] = "Le nom du client est obligatoire.";
            return RedirectToAction("Create", "Sales");
        }

        model.Lines ??= new List<BonLigneSlotViewModel>();

        var lignes = model.Lines
            .Where(l => l.ProductId > 0 && l.Quantity > 0)
            .Select(l => (
                l.ProductId,
                l.Quantity,
                l.DiscountPercent,
                l.DiscountAmount,
                l.DiscountType ?? ""))
            .ToList();

        if (!lignes.Any())
        {
            TempData["Error"] = "Ajoutez au moins un produit.";
            return RedirectToAction("Create", "Sales");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var (success, error, bonId) = await _bonService.CreateBonAsync(
            model.ClientNom.Trim(),
            model.ClientTelephone?.Trim(),
            null,
            lignes,
            userId,
            model.VendeurId,
            model.NumeroIdentite);

        if (!success)
        {
            TempData["Error"] = error;
            return RedirectToAction("Create", "Sales");
        }

        TempData["Success"] = "Bon créé avec succès. Le stock a été mis à jour.";
        TempData["NewBon"] = true;
        return RedirectToAction(nameof(Details), new { id = bonId });
    }

    // ─── Détails ─────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var bon = await _context.Bons
            .AsNoTracking()
            .Include(b => b.Lignes).ThenInclude(l => l.Product)
            .Include(b => b.Reglements)
            .Include(b => b.Vendeur)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bon == null) return NotFound();

        return View(bon);
    }

    // ─── Règlement ───────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Regler(int id)
    {
        var bon = await _context.Bons
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bon == null) return NotFound();
        if (bon.Statut == BonStatut.Solde || bon.Statut == BonStatut.Annule)
        {
            TempData["Error"] = "Ce bon ne peut plus être réglé.";
            return RedirectToAction(nameof(Details), new { id });
        }

        ViewBag.Bon = bon;
        return View(bon);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Regler(int id, decimal montant, PaymentMethod paymentMethod, string? paymentMethodAutre)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var (success, error) = await _bonService.ReglerBonAsync(id, montant, paymentMethod, userId);

        if (success)
        {
            if (paymentMethod == PaymentMethod.Autre)
            {
                var reglement = await _context.ReglementBons
                    .Where(r => r.BonId == id)
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefaultAsync();
                if (reglement != null)
                {
                    reglement.PaymentMethodAutre = paymentMethodAutre?.Trim();
                    await _context.SaveChangesAsync();
                }
            }

            TempData["Success"] = "Règlement enregistré avec succès.";
        }
        else
        {
            TempData["Error"] = error;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // ─── Ticket ──────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Ticket(int id)
    {
        var bon = await _context.Bons
            .AsNoTracking()
            .Include(b => b.Lignes).ThenInclude(l => l.Product)
            .Include(b => b.Reglements)
            .Include(b => b.Vendeur)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bon == null) return NotFound();
        return View(bon);
    }

    // ─── Annulation ──────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = $"{AppRoles.PharmacienTitulaire},{AppRoles.Administrateur},{AppRoles.Pharmacien}")]
    public async Task<IActionResult> AnnulerBon(int id, string? raison = null)
    {
        var bon = await _context.Bons
            .Include(b => b.Lignes)
                .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (bon == null) return NotFound();

        if (bon.Statut == BonStatut.Annule)
        {
            TempData["Error"] = "Ce bon est déjà annulé.";
            return RedirectToAction(nameof(Index));
        }

        if (bon.Statut == BonStatut.Solde)
        {
            TempData["Error"] = "Impossible d'annuler un bon soldé.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var userId = _userManager.GetUserId(User) ?? "";
        var user = await _userManager.FindByIdAsync(userId);
        var nomUser = user?.DisplayName ?? user?.UserName ?? userId;

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var marker = $"Bon {bon.Numero}";
            var sorties = await _context.StockMovements
                .Include(m => m.Batch)
                .Include(m => m.Product)
                .Where(m =>
                    m.Type == StockMovementType.Sortie
                    && m.Reason != null
                    && m.Reason.Contains(marker))
                .ToListAsync();

            if (sorties.Count > 0)
            {
                foreach (var mouvement in sorties)
                {
                    if (mouvement.Batch != null)
                        mouvement.Batch.Quantity += mouvement.Quantity;
                    if (mouvement.Product != null)
                        mouvement.Product.StockQuantity += mouvement.Quantity;

                    _context.StockMovements.Add(new StockMovement
                    {
                        ProductId = mouvement.ProductId,
                        BatchId = mouvement.BatchId,
                        Type = StockMovementType.Entree,
                        Quantity = mouvement.Quantity,
                        Reason = $"Restitution stock — Annulation bon #{bon.Numero} par {nomUser}",
                        OccurredAt = DateTime.Now,
                        UserId = userId
                    });
                }
            }
            else
            {
                foreach (var ligne in bon.Lignes)
                {
                    var product = await _context.Products
                        .Include(p => p.Batches)
                        .FirstOrDefaultAsync(p => p.Id == ligne.ProductId);
                    if (product == null) continue;

                    product.StockQuantity += ligne.Quantity;

                    var lot = product.Batches
                        .OrderBy(b => b.ExpirationDate)
                        .ThenBy(b => b.Id)
                        .FirstOrDefault();

                    if (lot != null)
                    {
                        lot.Quantity += ligne.Quantity;
                        _context.StockMovements.Add(new StockMovement
                        {
                            ProductId = product.Id,
                            BatchId = lot.Id,
                            Type = StockMovementType.Entree,
                            Quantity = ligne.Quantity,
                            Reason = $"Restitution stock — Annulation bon #{bon.Numero} par {nomUser}",
                            OccurredAt = DateTime.Now,
                            UserId = userId
                        });
                    }
                }
            }

            bon.Statut = BonStatut.Annule;
            var extra = string.IsNullOrWhiteSpace(raison)
                ? $"\nAnnulé par {nomUser} le {DateTime.Now:dd/MM/yyyy HH:mm}"
                : $"\nAnnulé par {nomUser} le {DateTime.Now:dd/MM/yyyy HH:mm} — {raison.Trim()}";
            var combined = string.IsNullOrWhiteSpace(bon.Notes) ? extra.Trim() : bon.Notes + extra;
            bon.Notes = combined.Length <= 500 ? combined : combined[..500];

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Success"] = $"Bon #{bon.Numero} annulé. Stock restitué.";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Erreur lors de l'annulation du bon {BonId}", id);
            TempData["Error"] = "Erreur lors de l'annulation : " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

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
