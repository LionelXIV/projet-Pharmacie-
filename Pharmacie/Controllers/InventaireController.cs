using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Services;

namespace Pharmacie.Controllers;

[Authorize(Roles = AppRoles.PharmacienTitulaire + "," + AppRoles.Pharmacien + "," + AppRoles.Administrateur)]
public class InventaireController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly InventoryService _inventoryService;

    public InventaireController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        InventoryService inventoryService)
    {
        _context = context;
        _userManager = userManager;
        _inventoryService = inventoryService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? categorieId = null)
    {
        var query = _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Where(p => p.IsActive && (p.Category == null || !p.Category.EstHorsSysteme));

        if (categorieId.HasValue)
            query = query.Where(p => p.CategoryId == categorieId.Value);

        var produits = await query
            .OrderBy(p => p.Category != null ? p.Category.Name : "")
            .ThenBy(p => p.CommercialName)
            .Select(p => new InventaireItemViewModel
            {
                ProductId = p.Id,
                Nom = p.CommercialName,
                Cip = p.Cip,
                Categorie = p.Category != null ? p.Category.Name : "Sans catégorie",
                StockLogiciel = p.StockQuantity,
                StockPhysique = null,
                Ecart = null
            })
            .ToListAsync();

        ViewBag.Categories = await _context.Categories
            .AsNoTracking()
            .Where(c => !c.EstHorsSysteme)
            .OrderBy(c => c.Name)
            .ToListAsync();
        ViewBag.CategorieId = categorieId;

        return View(produits);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValiderInventaire(List<InventaireAjustement> ajustements)
    {
        var userId = _userManager.GetUserId(User) ?? "";
        var nbAjustements = 0;
        var erreurs = new List<string>();

        foreach (var aj in (ajustements ?? new List<InventaireAjustement>())
                     .Where(a => a.StockPhysique.HasValue))
        {
            var product = await _context.Products
                .Include(p => p.Batches)
                .FirstOrDefaultAsync(p => p.Id == aj.ProductId);
            if (product == null)
                continue;

            var physique = aj.StockPhysique!.Value;
            if (physique < 0)
            {
                erreurs.Add($"{product.CommercialName} : stock physique invalide.");
                continue;
            }

            var ecart = physique - product.StockQuantity;
            if (ecart == 0)
                continue;

            var reason =
                $"Inventaire physique — Stock logiciel : {product.StockQuantity} | Stock physique : {physique}";

            try
            {
                var (ok, error) = await AppliquerEcartAsync(product, ecart, reason, userId);
                if (ok)
                    nbAjustements++;
                else
                    erreurs.Add($"{product.CommercialName} : {error}");
            }
            catch (Exception ex)
            {
                erreurs.Add($"{product.CommercialName} : {ex.Message}");
            }
        }

        if (erreurs.Count > 0)
            TempData["Warning"] =
                $"{nbAjustements} ajustement(s) OK. Erreurs : {string.Join(", ", erreurs)}";
        else
            TempData["Success"] = $"{nbAjustements} produit(s) ajusté(s) avec succès.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<(bool Ok, string? Error)> AppliquerEcartAsync(
        Product product,
        int ecart,
        string reason,
        string userId)
    {
        if (ecart > 0)
        {
            var lot = product.Batches.OrderByDescending(b => b.Id).FirstOrDefault();
            if (lot == null)
            {
                return await _inventoryService.RecordEntreeAsync(
                    product.Id,
                    $"INV-{product.Id}-{DateTime.Now:yyyyMMddHHmmss}",
                    DateTime.Today.AddYears(2),
                    ecart,
                    reason,
                    userId);
            }

            return await _inventoryService.RecordAjustementAsync(lot.Id, ecart, reason, userId);
        }

        var remaining = Math.Abs(ecart);
        var lots = product.Batches
            .Where(b => b.Quantity > 0)
            .OrderBy(b => b.ExpirationDate)
            .ThenBy(b => b.Id)
            .ToList();

        foreach (var lot in lots)
        {
            if (remaining <= 0)
                break;

            var take = Math.Min(lot.Quantity, remaining);
            var (ok, error) = await _inventoryService.RecordAjustementAsync(lot.Id, -take, reason, userId);
            if (!ok)
                return (false, error);
            remaining -= take;
        }

        if (remaining > 0)
            return (false, "Lots insuffisants pour absorber l'écart.");

        return (true, null);
    }
}
