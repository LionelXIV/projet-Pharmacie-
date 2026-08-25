using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Reporting;
using Pharmacie.Services;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.Sales)]
public class SalesController : Controller
{
    private const int IndexPageSize = 50;

    private readonly ApplicationDbContext _context;
    private readonly SaleService _sales;
    private readonly CaisseService _caisseService;
    private readonly ILogger<SalesController> _logger;
    private readonly IOptions<FeatureFlags> _features;
    private readonly UserManager<ApplicationUser> _userManager;

    public SalesController(
        ApplicationDbContext context,
        SaleService sales,
        CaisseService caisseService,
        ILogger<SalesController> logger,
        IOptions<FeatureFlags> features,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _sales = sales;
        _caisseService = caisseService;
        _logger = logger;
        _features = features;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index([FromQuery] SaleListFilters? filter, int page = 1)
    {
        filter ??= new SaleListFilters();
        if (page < 1)
            page = 1;

        var q = _context.Sales
            .AsNoTracking()
            .Include(s => s.Lines).ThenInclude(l => l.Product!).ThenInclude(p => p.Category)
            .Include(s => s.Vendeur)
            .AsQueryable();

        DateTime? fromDay = filter.From?.Date;
        DateTime? toDay = filter.To?.Date;
        if (fromDay.HasValue && toDay.HasValue && fromDay > toDay)
            (fromDay, toDay) = (toDay, fromDay);

        if (fromDay.HasValue)
        {
            var from = fromDay.Value;
            q = q.Where(s => s.SoldAt >= from);
        }

        if (toDay.HasValue)
        {
            var toExclusive = toDay.Value.AddDays(1);
            q = q.Where(s => s.SoldAt < toExclusive);
        }

        if (!string.IsNullOrEmpty(filter.UserId))
            q = q.Where(s => s.UserId == filter.UserId);

        var totalCount = await q.CountAsync();
        var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)IndexPageSize);
        if (page > totalPages)
            page = totalPages;

        var list = await q
            .OrderByDescending(s => s.SoldAt)
            .ThenByDescending(s => s.Id)
            .Skip((page - 1) * IndexPageSize)
            .Take(IndexPageSize)
            .ToListAsync();

        ViewBag.SaisieVentePasseeActive = _features.Value.SaisieVentePassee;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = totalCount;
        ViewBag.UserLabels = await UserDisplayResolver.LoadLabelsByIdAsync(_context, list.Select(s => s.UserId));
        await PopulateSaleFilterUsersAsync(filter.UserId);
        return View(new SaleIndexPageViewModel { Filter = filter, Sales = list });
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var sale = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null)
            return NotFound();

        ViewBag.RecordedBy = string.IsNullOrEmpty(sale.UserId)
            ? "—"
            : UserDisplayResolver.Resolve(
                await UserDisplayResolver.LoadLabelsByIdAsync(_context, new[] { sale.UserId }),
                sale.UserId);

        return View(sale);
    }

    public async Task<IActionResult> Ticket(int id)
    {
        var sale = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Lines)
                .ThenInclude(l => l.Product)
            .Include(s => s.Vendeur)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sale == null) return NotFound();

        return View(sale);
    }

    [HttpGet]
    public async Task<IActionResult> FicheVente(int id)
    {
        var sale = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Lines)
                .ThenInclude(l => l.Product!)
                    .ThenInclude(p => p.Category)
            .Include(s => s.Vendeur)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sale == null) return NotFound();

        ViewBag.RecordedBy = string.IsNullOrEmpty(sale.UserId)
            ? "—"
            : UserDisplayResolver.Resolve(
                await UserDisplayResolver.LoadLabelsByIdAsync(_context, new[] { sale.UserId }),
                sale.UserId);

        return View(sale);
    }

    public async Task<IActionResult> DetailsCsv(int? id)
    {
        if (id == null)
            return NotFound();

        var sale = await _context.Sales
            .AsNoTracking()
            .Include(s => s.Lines)
            .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null)
            return NotFound();

        var labels = await UserDisplayResolver.LoadLabelsByIdAsync(
            _context,
            string.IsNullOrEmpty(sale.UserId) ? Array.Empty<string>() : new[] { sale.UserId });
        var recordedBy = string.IsNullOrEmpty(sale.UserId)
            ? ""
            : UserDisplayResolver.Resolve(labels, sale.UserId);

        var sb = ReportCsvFormatter.CreateBuilder();
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("N° vente"),
            ReportCsvFormatter.Escape("Date vente"),
            ReportCsvFormatter.Escape("Enregistré par"),
            ReportCsvFormatter.Escape("Moyen de paiement"),
            ReportCsvFormatter.Escape("Notes")));
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.IntInvariant(sale.Id),
            ReportCsvFormatter.Escape(sale.SoldAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),
            ReportCsvFormatter.Escape(recordedBy),
            ReportCsvFormatter.Escape(PaymentMethodDisplay.GetName(sale.PaymentMethod)),
            ReportCsvFormatter.Escape(sale.Notes ?? "")));

        sb.AppendLine();
        sb.AppendLine(ReportCsvFormatter.Join(
            ReportCsvFormatter.Escape("Ligne"),
            ReportCsvFormatter.Escape("N° produit"),
            ReportCsvFormatter.Escape("Produit"),
            ReportCsvFormatter.Escape("Prix unit. (FCFA)"),
            ReportCsvFormatter.Escape("Qté"),
            ReportCsvFormatter.Escape("Sous-total (FCFA)")));

        var lineNo = 1;
        foreach (var l in sale.Lines.OrderBy(x => x.Id))
        {
            var sub = l.Quantity * l.UnitPrice;
            sb.AppendLine(ReportCsvFormatter.Join(
                ReportCsvFormatter.IntInvariant(lineNo++),
                ReportCsvFormatter.IntInvariant(l.ProductId),
                ReportCsvFormatter.Escape(l.Product?.CommercialName ?? ""),
                ReportCsvFormatter.FcfaCsvAmount(l.UnitPrice),
                ReportCsvFormatter.IntInvariant(l.Quantity),
                ReportCsvFormatter.FcfaCsvAmount(sub)));
        }

        return ReportCsvFormatter.FileResult(this, sb.ToString(), $"vente-{sale.Id}-lignes");
    }

    public async Task<IActionResult> Create()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var isAdmin = User.IsInRole(AppRoles.Administrateur);

        if (!isAdmin)
        {
            var session = await _caisseService.GetSessionOuverteAsync(userId);
            if (session == null)
            {
                TempData["Warning"] = "Ouvrez une caisse avant de faire une vente.";
                return RedirectToAction("Index", "Caisse");
            }

            ViewBag.SessionCaisse = session;
            ViewBag.SessionCaisseId = session.Id;
            ViewBag.SessionCaisseNom = session.NomCaisse;
        }
        else
        {
            var session = await _caisseService.GetSessionOuverteAsync(userId);
            if (session != null)
            {
                ViewBag.SessionCaisse = session;
                ViewBag.SessionCaisseId = session.Id;
                ViewBag.SessionCaisseNom = session.NomCaisse;
            }
        }

        await PopulateVendeursForPosAsync();
        return View(new SaleCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SaleCreateViewModel model)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var isAdmin = User.IsInRole(AppRoles.Administrateur);
            var session = await _caisseService.GetSessionOuverteAsync(userId);
            if (!isAdmin && session == null)
            {
                TempData["Warning"] = "Ouvrez une caisse avant de faire une vente.";
                return RedirectToAction("Index", "Caisse");
            }

            // Si SoldAt n'est pas posté / mal parsé, utiliser l'heure serveur
            if (model.SoldAt == default)
                model.SoldAt = DateTime.Now;

            var slots = model.Lines ?? new List<SaleLineSlotViewModel>();
            var lines = slots
                .Where(l => l.ProductId > 0 && l.Quantity > 0)
                .Select(l => (l.ProductId, l.Quantity))
                .ToList();

            if (lines.Count == 0)
                ModelState.AddModelError(string.Empty, "Ajoutez au moins une ligne avec un produit et une quantité.");

            if (!model.VendeurId.HasValue || model.VendeurId.Value <= 0)
                ModelState.AddModelError(nameof(model.VendeurId), "Veuillez sélectionner le vendeur.");
            else
            {
                var vendeurOk = await _context.Vendeurs.AnyAsync(v => v.Id == model.VendeurId && v.IsActif);
                if (!vendeurOk)
                    ModelState.AddModelError(nameof(model.VendeurId), "Vendeur invalide ou inactif.");
            }

            if (ModelState.IsValid)
            {
                var (ok, error, saleId) = await _sales.RecordSaleAsync(
                    model.SoldAt,
                    model.Notes,
                    lines,
                    userId,
                    model.PaymentMethod);

                if (ok && saleId.HasValue)
                {
                    var sale = await _context.Sales
                        .Include(s => s.Lines)
                        .FirstOrDefaultAsync(s => s.Id == saleId.Value);

                    if (sale != null)
                    {
                        sale.VendeurId = model.VendeurId;
                        sale.PaymentMethodAutre = model.PaymentMethod == PaymentMethod.Autre
                            ? model.PaymentMethodAutre?.Trim()
                            : null;

                        // Appliquer les remises par ligne (hors SaleService)
                        var orderedSaleLines = sale.Lines.OrderBy(l => l.Id).ToList();
                        for (var i = 0; i < slots.Count && i < orderedSaleLines.Count; i++)
                        {
                            var slot = slots[i];
                            if (slot.ProductId <= 0 || slot.Quantity <= 0)
                                continue;

                            var discountType = slot.DiscountType?.Trim() ?? "";
                            if (discountType is "percent" or "amount")
                            {
                                var saleLine = orderedSaleLines[i];
                                saleLine.DiscountType = discountType;
                                saleLine.DiscountPercent = discountType == "percent" ? slot.DiscountPercent : 0;
                                saleLine.DiscountAmount = discountType == "amount" ? slot.DiscountAmount : 0;
                            }
                        }

                        if (model.PaymentMethod == PaymentMethod.Especes && model.MontantEncaisse > 0)
                        {
                            var totalVente = sale.Lines.Sum(CaisseService.LineTotal);
                            sale.MontantEncaisse = model.MontantEncaisse;
                            sale.MonnaieRendue = Math.Max(0, model.MontantEncaisse - totalVente);
                        }
                        else
                        {
                            sale.MontantEncaisse = 0;
                            sale.MonnaieRendue = 0;
                        }

                        await _context.SaveChangesAsync();
                    }

                    if (session != null && !isAdmin)
                        await _caisseService.LierVenteAsync(session.Id, saleId.Value);

                    TempData["NewSale"] = true;
                    return RedirectToAction(nameof(Details), new { id = saleId.Value });
                }

                ModelState.AddModelError(string.Empty, error ?? "Vente impossible.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création de la vente (PaymentMethod={PM}, VendeurId={VId}, Lines={Lines})",
                model.PaymentMethod, model.VendeurId, model.Lines?.Count ?? 0);
            ModelState.AddModelError(string.Empty, $"Une erreur inattendue s'est produite : {ex.Message}");
        }

        if (model.Lines == null || model.Lines.Count == 0)
            model.Lines = new List<SaleLineSlotViewModel> { new() };

        var reopenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        var reopenSession = await _caisseService.GetSessionOuverteAsync(reopenUserId);
        if (reopenSession != null)
        {
            ViewBag.SessionCaisse = reopenSession;
            ViewBag.SessionCaisseId = reopenSession.Id;
            ViewBag.SessionCaisseNom = reopenSession.NomCaisse;
        }

        await PopulateVendeursForPosAsync(model.VendeurId);
        return View(model);
    }

    [HttpGet]
    [Authorize(Roles = "PharmacienTitulaire,Pharmacien")]
    public async Task<IActionResult> CreatePassee()
    {
        if (!_features.Value.SaisieVentePassee)
            return NotFound();

        await PopulateVendeursForPosAsync();
        return View(new SaleCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "PharmacienTitulaire,Pharmacien")]
    public async Task<IActionResult> CreatePassee(SaleCreateViewModel model)
    {
        if (!_features.Value.SaisieVentePassee)
            return NotFound();

        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

            if (model.SoldAt == default)
                ModelState.AddModelError(nameof(model.SoldAt), "Sélectionnez la date de la vente.");
            else if (model.SoldAt.Date > DateTime.Today)
                ModelState.AddModelError(nameof(model.SoldAt), "La date de la vente ne peut pas être dans le futur.");

            var slots = model.Lines ?? new List<SaleLineSlotViewModel>();
            var lines = slots
                .Where(l => l.ProductId > 0 && l.Quantity > 0)
                .Select(l => (l.ProductId, l.Quantity))
                .ToList();

            if (lines.Count == 0)
                ModelState.AddModelError(string.Empty, "Ajoutez au moins une ligne avec un produit et une quantité.");

            if (!model.VendeurId.HasValue || model.VendeurId.Value <= 0)
                ModelState.AddModelError(nameof(model.VendeurId), "Veuillez sélectionner le vendeur.");
            else
            {
                var vendeurOk = await _context.Vendeurs.AnyAsync(v => v.Id == model.VendeurId && v.IsActif);
                if (!vendeurOk)
                    ModelState.AddModelError(nameof(model.VendeurId), "Vendeur invalide ou inactif.");
            }

            if (ModelState.IsValid)
            {
                var (ok, error, saleId) = await _sales.RecordSaleAsync(
                    model.SoldAt,
                    model.Notes,
                    lines,
                    userId,
                    model.PaymentMethod);

                if (ok && saleId.HasValue)
                {
                    var sale = await _context.Sales
                        .Include(s => s.Lines)
                        .FirstOrDefaultAsync(s => s.Id == saleId.Value);

                    if (sale != null)
                    {
                        sale.IsRegularisation = true;
                        sale.VendeurId = model.VendeurId;
                        sale.PaymentMethodAutre = model.PaymentMethod == PaymentMethod.Autre
                            ? model.PaymentMethodAutre?.Trim()
                            : null;

                        var pricedSlots = slots
                            .Where(l => l.ProductId > 0 && l.Quantity > 0)
                            .ToList();
                        var orderedSaleLines = sale.Lines.OrderBy(l => l.Id).ToList();
                        for (var i = 0; i < pricedSlots.Count && i < orderedSaleLines.Count; i++)
                        {
                            var slot = pricedSlots[i];
                            var saleLine = orderedSaleLines[i];
                            saleLine.UnitPrice = slot.UnitPrice;

                            var discountType = slot.DiscountType?.Trim() ?? "";
                            if (discountType is "percent" or "amount")
                            {
                                saleLine.DiscountType = discountType;
                                saleLine.DiscountPercent = discountType == "percent" ? slot.DiscountPercent : 0;
                                saleLine.DiscountAmount = discountType == "amount" ? slot.DiscountAmount : 0;
                            }
                        }

                        await _context.SaveChangesAsync();
                    }

                    TempData["NewSale"] = true;
                    return RedirectToAction(nameof(Details), new { id = saleId.Value });
                }

                ModelState.AddModelError(string.Empty, error ?? "Vente impossible.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la saisie d'une vente passée (PaymentMethod={PM}, VendeurId={VId}, Lines={Lines})",
                model.PaymentMethod, model.VendeurId, model.Lines?.Count ?? 0);
            ModelState.AddModelError(string.Empty, $"Une erreur inattendue s'est produite : {ex.Message}");
        }

        if (model.Lines == null || model.Lines.Count == 0)
            model.Lines = new List<SaleLineSlotViewModel> { new() };

        await PopulateVendeursForPosAsync(model.VendeurId);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "PharmacienTitulaire,Pharmacien,Caissier,AssistantPharmacien")]
    public async Task<IActionResult> AnnulerVente(int id, string? raison = null)
    {
        var sale = await _context.Sales
            .Include(s => s.Lines)
                .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(s => s.Id == id);
        if (sale == null)
            return NotFound();

        if (sale.IsAnnulee)
        {
            TempData["Error"] = "Cette vente est déjà annulée.";
            return RedirectToAction(nameof(Index));
        }

        var userId = _userManager.GetUserId(User) ?? "";
        var user = await _userManager.FindByIdAsync(userId);
        var nomUser = user?.DisplayName ?? user?.UserName ?? userId;

        if (!User.IsInRole(AppRoles.PharmacienTitulaire)
            && !User.IsInRole(AppRoles.Pharmacien))
        {
            if (sale.UserId != userId)
            {
                TempData["Error"] = "Vous ne pouvez annuler que vos propres ventes.";
                return RedirectToAction(nameof(Index));
            }
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            sale.IsAnnulee = true;
            sale.DateAnnulation = DateTime.Now;
            sale.AnnuleeParUserId = userId;
            sale.AnnuleeParNom = nomUser;
            sale.RaisonAnnulation = string.IsNullOrWhiteSpace(raison)
                ? "Annulation manuelle"
                : raison.Trim();

            var sorties = await _context.StockMovements
                .Include(m => m.Batch)
                .Include(m => m.Product)
                .Where(m => m.SaleId == sale.Id && m.Type == StockMovementType.Sortie)
                .ToListAsync();

            if (sorties.Count == 0)
            {
                var productIds = sale.Lines.Select(l => l.ProductId).Distinct().ToList();
                var from = sale.SoldAt.AddSeconds(-30);
                var to = sale.SoldAt.AddSeconds(30);
                var marker = $"#{sale.Id}";
                sorties = await _context.StockMovements
                    .Include(m => m.Batch)
                    .Include(m => m.Product)
                    .Where(m =>
                        m.Type == StockMovementType.Sortie
                        && productIds.Contains(m.ProductId)
                        && m.OccurredAt >= from
                        && m.OccurredAt <= to
                        && (m.UserId == sale.UserId || sale.UserId == null)
                        && (m.Reason == "Vente"
                            || (m.Reason != null && m.Reason.Contains(marker))))
                    .ToListAsync();
            }

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
                        Reason = $"Restitution stock — Annulation vente #{id} par {nomUser}",
                        OccurredAt = DateTime.Now,
                        UserId = userId,
                        SaleId = sale.Id
                    });
                }
            }
            else
            {
                foreach (var ligne in sale.Lines)
                {
                    var product = await _context.Products
                        .FirstOrDefaultAsync(p => p.Id == ligne.ProductId);
                    if (product == null)
                        continue;

                    product.StockQuantity += ligne.Quantity;

                    var lot = await _context.ProductBatches
                        .Where(b => b.ProductId == ligne.ProductId)
                        .OrderBy(b => b.ExpirationDate)
                        .ThenBy(b => b.Id)
                        .FirstOrDefaultAsync();

                    if (lot != null)
                    {
                        lot.Quantity += ligne.Quantity;
                        _context.StockMovements.Add(new StockMovement
                        {
                            ProductId = ligne.ProductId,
                            BatchId = lot.Id,
                            Type = StockMovementType.Entree,
                            Quantity = ligne.Quantity,
                            Reason = $"Restitution stock — Annulation vente #{sale.Id}",
                            OccurredAt = DateTime.Now,
                            UserId = userId,
                            SaleId = sale.Id
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Success"] = $"Vente #{id} annulée. Stock restitué.";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Erreur lors de l'annulation de la vente {SaleId}", id);
            TempData["Error"] = "Erreur lors de l'annulation : " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateVendeursForPosAsync(int? selectedId = null)
    {
        ViewBag.Vendeurs = await _context.Vendeurs
            .AsNoTracking()
            .Where(v => v.IsActif)
            .OrderBy(v => v.Nom)
            .Select(v => new { v.Id, v.Nom, v.CouleurTicket })
            .ToListAsync();
        ViewBag.SelectedVendeurId = selectedId;
    }

    private async Task PopulateVendeursAsync(int? selectedId = null)
    {
        var items = await _context.Vendeurs
            .AsNoTracking()
            .Where(v => v.IsActif)
            .OrderBy(v => v.Nom)
            .Select(v => new SelectListItem
            {
                Value = v.Id.ToString(),
                Text = v.CouleurTicket != null ? $"{v.Nom} ({v.CouleurTicket})" : v.Nom,
                Selected = selectedId.HasValue && v.Id == selectedId.Value
            })
            .ToListAsync();
        ViewBag.Vendeurs = items;
    }

    private async Task PopulateSaleFilterUsersAsync(string? selectedUserId)
    {
        var userIds = await _context.Sales
            .AsNoTracking()
            .Where(s => s.UserId != null && s.UserId != "")
            .Select(s => s.UserId!)
            .Distinct()
            .ToListAsync();

        var users = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .OrderBy(u => u.Email)
            .ThenBy(u => u.UserName)
            .ToListAsync();

        var userItems = users
            .Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = UserDisplayResolver.Format(u.Email, u.UserName, u.DisplayName),
                Selected = u.Id == selectedUserId
            })
            .ToList();

        ViewData["FilterUserId"] = userItems;
    }
}
