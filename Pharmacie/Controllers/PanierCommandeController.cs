using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Authorization;
using Pharmacie.Data;
using Pharmacie.Models;

namespace Pharmacie.Controllers;

[Authorize(Roles =
    AppRoles.PharmacienTitulaire + "," +
    AppRoles.Pharmacien + "," +
    AppRoles.Administrateur)]
public class PanierCommandeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public PanierCommandeController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId();
        if (userId == null)
            return Challenge();

        var panier = await GetOrCreatePanierAsync(userId);

        var lignes = await _context.PanierCommandeLignes
            .AsNoTracking()
            .Include(l => l.Product!)
                .ThenInclude(p => p.Supplier)
            .Include(l => l.Supplier)
            .Where(l => l.PanierCommandeId == panier.Id)
            .ToListAsync();

        lignes = lignes
            .OrderBy(l => l.Source)
            .ThenBy(l => l.Product?.CommercialName ?? "")
            .ToList();

        var parFournisseur = lignes
            .GroupBy(l => l.Product?.Supplier?.Name
                          ?? l.Supplier?.Name
                          ?? "Sans fournisseur")
            .OrderBy(g => g.Key)
            .Select(g => new PanierFournisseurGroupe
            {
                Nom = g.Key,
                Lignes = g.ToList()
            })
            .ToList();

        ViewBag.PanierId = panier.Id;
        ViewBag.ParFournisseur = parFournisseur;
        ViewBag.NbLignes = lignes.Count;
        ViewBag.NbFournisseurs = parFournisseur.Count;

        return View(lignes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AjouterSuggestions(string source)
    {
        var userId = CurrentUserId();
        if (userId == null)
            return Challenge();

        var panier = await GetOrCreatePanierAsync(userId);
        var produits = await LoadSuggestionProductsAsync(source);

        var existants = await _context.PanierCommandeLignes
            .Where(l => l.PanierCommandeId == panier.Id)
            .ToListAsync();
        var byProduct = existants.ToDictionary(l => l.ProductId);

        var nbAjoutes = 0;
        var nbFusionnes = 0;

        foreach (var product in produits)
        {
            var qteConseillee = product.StockMaximum > 0
                ? Math.Max(0, product.StockMaximum - product.StockQuantity)
                : Math.Max(1, product.AlertThreshold * 2 - product.StockQuantity);

            if (qteConseillee <= 0)
                qteConseillee = 1;

            if (byProduct.TryGetValue(product.Id, out var existant))
            {
                if (qteConseillee > existant.QuantiteFinale)
                {
                    existant.QuantiteFinale = qteConseillee;
                    existant.QuantiteConseillee = qteConseillee;
                    existant.Source = MergeSource(existant.Source, source);
                }
                else
                {
                    existant.Source = MergeSource(existant.Source, source);
                }
                nbFusionnes++;
            }
            else
            {
                var ligne = new PanierCommandeLigne
                {
                    PanierCommandeId = panier.Id,
                    ProductId = product.Id,
                    SupplierId = product.SupplierId,
                    QuantiteConseillee = qteConseillee,
                    QuantiteFinale = qteConseillee,
                    Source = source,
                    Selectionne = true
                };
                _context.PanierCommandeLignes.Add(ligne);
                byProduct[product.Id] = ligne;
                nbAjoutes++;
            }
        }

        panier.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();

        TempData["Success"] =
            $"{nbAjoutes} produit(s) ajouté(s)" +
            (nbFusionnes > 0 ? $", {nbFusionnes} fusionné(s)" : "") +
            " dans le panier.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AjouterProduit(int productId, int quantite = 1)
    {
        var userId = CurrentUserId();
        if (userId == null)
            return Challenge();

        if (productId <= 0 || quantite <= 0)
        {
            TempData["Error"] = "Produit ou quantité invalide.";
            return RedirectToAction(nameof(Index));
        }

        var panier = await GetOrCreatePanierAsync(userId);

        var product = await _context.Products
            .Include(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.Id == productId);

        if (product == null)
            return NotFound();

        var existant = await _context.PanierCommandeLignes
            .FirstOrDefaultAsync(l =>
                l.PanierCommandeId == panier.Id && l.ProductId == productId);

        if (existant != null)
        {
            existant.QuantiteFinale += quantite;
            existant.Source = MergeSource(existant.Source, "Manuel");
        }
        else
        {
            _context.PanierCommandeLignes.Add(new PanierCommandeLigne
            {
                PanierCommandeId = panier.Id,
                ProductId = productId,
                SupplierId = product.SupplierId,
                QuantiteConseillee = quantite,
                QuantiteFinale = quantite,
                Source = "Manuel",
                Selectionne = true
            });
        }

        panier.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();

        TempData["Success"] = $"{product.CommercialName} ajouté au panier.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AjouterProduitAjax(int productId, int quantite = 1)
    {
        var userId = CurrentUserId();
        if (userId == null)
            return Json(new { success = false, message = "Non authentifié" });

        if (productId <= 0 || quantite <= 0)
            return Json(new { success = false, message = "Produit ou quantité invalide" });

        try
        {
            var panier = await GetOrCreatePanierAsync(userId);

            var product = await _context.Products
                .Include(p => p.Supplier)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
                return Json(new { success = false, message = "Produit introuvable" });

            var existant = await _context.PanierCommandeLignes
                .FirstOrDefaultAsync(l =>
                    l.PanierCommandeId == panier.Id && l.ProductId == productId);

            if (existant != null)
            {
                existant.QuantiteFinale += quantite;
                existant.Source = MergeSource(existant.Source, "Manuel");
            }
            else
            {
                _context.PanierCommandeLignes.Add(new PanierCommandeLigne
                {
                    PanierCommandeId = panier.Id,
                    ProductId = productId,
                    SupplierId = product.SupplierId,
                    QuantiteConseillee = quantite,
                    QuantiteFinale = quantite,
                    Source = "Manuel",
                    Selectionne = true
                });
            }

            panier.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            var nbTotal = await _context.PanierCommandeLignes
                .CountAsync(l => l.PanierCommandeId == panier.Id);

            return Json(new
            {
                success = true,
                nbTotal,
                message = product.CommercialName + " ajouté au panier"
            });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "Erreur lors de l'ajout" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ModifierQuantite(int ligneId, int quantite)
    {
        var ligne = await GetOwnedLigneAsync(ligneId);
        if (ligne == null)
            return NotFound();

        if (quantite <= 0)
            _context.PanierCommandeLignes.Remove(ligne);
        else
            ligne.QuantiteFinale = quantite;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SupprimerLigne(int ligneId)
    {
        var ligne = await GetOwnedLigneAsync(ligneId);
        if (ligne != null)
        {
            _context.PanierCommandeLignes.Remove(ligne);
            await _context.SaveChangesAsync();
        }

        TempData["Success"] = "Produit retiré du panier.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ViderPanier()
    {
        var userId = CurrentUserId();
        if (userId == null)
            return Challenge();

        var panier = await _context.PanierCommandes
            .Include(p => p.Lignes)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Statut == "EnCours");

        if (panier != null)
        {
            _context.PanierCommandeLignes.RemoveRange(panier.Lignes);
            panier.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        TempData["Success"] = "Panier vidé.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValiderPanier()
    {
        var userId = CurrentUserId();
        if (userId == null)
            return Challenge();

        var panier = await _context.PanierCommandes
            .Include(p => p.Lignes)
                .ThenInclude(l => l.Product!)
                    .ThenInclude(p => p.Supplier)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Statut == "EnCours");

        if (panier == null || !panier.Lignes.Any(l => l.Selectionne && l.QuantiteFinale > 0))
        {
            TempData["Error"] = "Le panier est vide.";
            return RedirectToAction(nameof(Index));
        }

        var parFournisseur = panier.Lignes
            .Where(l => l.Selectionne && l.QuantiteFinale > 0 && l.Product != null)
            .GroupBy(l => l.Product!.SupplierId)
            .ToList();

        var commandesCreees = 0;
        var sansFournisseur = 0;

        foreach (var groupe in parFournisseur)
        {
            if (groupe.Key <= 0)
            {
                sansFournisseur += groupe.Count();
                continue;
            }

            var commande = new PurchaseOrder
            {
                SupplierId = groupe.Key,
                OrderDate = DateTime.Now,
                Status = PurchaseOrderStatus.Brouillon,
                Notes = "Créée depuis le panier de commande",
                Lines = groupe.Select(l => new PurchaseOrderLine
                {
                    ProductId = l.ProductId,
                    QuantityOrdered = l.QuantiteFinale
                }).ToList()
            };

            _context.PurchaseOrders.Add(commande);
            commandesCreees++;
        }

        if (commandesCreees == 0)
        {
            TempData["Error"] = sansFournisseur > 0
                ? "Impossible de créer une commande : les produits n'ont pas de fournisseur."
                : "Le panier est vide.";
            return RedirectToAction(nameof(Index));
        }

        panier.Statut = "Valide";
        panier.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();

        TempData["Success"] =
            $"{commandesCreees} commande(s) créée(s) en brouillon. " +
            "Vérifiez dans Commerce → Commandes fournisseurs." +
            (sansFournisseur > 0
                ? $" {sansFournisseur} produit(s) sans fournisseur ignoré(s)."
                : "");

        return RedirectToAction("Index", "PurchaseOrders");
    }

    [HttpGet]
    public async Task<IActionResult> NbLignes()
    {
        var userId = CurrentUserId();
        if (userId == null)
            return Json(new { nb = 0 });

        var nb = await _context.PanierCommandeLignes
            .CountAsync(l =>
                l.PanierCommande != null
                && l.PanierCommande.UserId == userId
                && l.PanierCommande.Statut == "EnCours");

        return Json(new { nb });
    }

    private string? CurrentUserId() => _userManager.GetUserId(User);

    private async Task<PanierCommande> GetOrCreatePanierAsync(string userId)
    {
        var panier = await _context.PanierCommandes
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Statut == "EnCours");

        if (panier == null)
        {
            panier = new PanierCommande
            {
                UserId = userId,
                Statut = "EnCours"
            };
            _context.PanierCommandes.Add(panier);
            await _context.SaveChangesAsync();
        }

        return panier;
    }

    private async Task<PanierCommandeLigne?> GetOwnedLigneAsync(int ligneId)
    {
        var userId = CurrentUserId();
        if (userId == null)
            return null;

        return await _context.PanierCommandeLignes
            .Include(l => l.PanierCommande)
            .FirstOrDefaultAsync(l =>
                l.Id == ligneId
                && l.PanierCommande != null
                && l.PanierCommande.UserId == userId
                && l.PanierCommande.Statut == "EnCours");
    }

    private async Task<List<Product>> LoadSuggestionProductsAsync(string source)
    {
        IQueryable<Product> Catalog() => _context.Products
            .Include(p => p.Supplier)
            .Where(p => p.IsActive
                && (p.Category == null || !p.Category.EstHorsSysteme));

        switch (source)
        {
            case "Rupture":
                return await Catalog()
                    .Where(p => p.StockQuantity <= 0)
                    .ToListAsync();

            case "StockFaible":
                return await Catalog()
                    .Where(p => p.StockQuantity > 0
                        && p.AlertThreshold > 0
                        && p.StockQuantity <= p.AlertThreshold)
                    .ToListAsync();

            case "TopVente":
            {
                var trente = DateTime.Today.AddDays(-30);
                var topIds = await _context.SaleLines
                    .Where(sl => sl.Sale != null
                        && sl.Sale.SoldAt >= trente
                        && !sl.Sale.IsAnnulee
                        && !sl.Sale.IsAdminTest)
                    .GroupBy(sl => sl.ProductId)
                    .Select(g => new { ProductId = g.Key, Qty = g.Sum(sl => sl.Quantity) })
                    .OrderByDescending(x => x.Qty)
                    .Take(20)
                    .Select(x => x.ProductId)
                    .ToListAsync();

                return await Catalog()
                    .Where(p => topIds.Contains(p.Id)
                        && p.StockQuantity <= p.AlertThreshold)
                    .ToListAsync();
            }

            default:
                return new List<Product>();
        }
    }

    private static string MergeSource(string current, string added)
    {
        if (string.IsNullOrWhiteSpace(added))
            return current ?? "";
        if (string.IsNullOrWhiteSpace(current))
            return added;
        if (current.Contains(added, StringComparison.OrdinalIgnoreCase))
            return current;

        var merged = current + "+" + added;
        return merged.Length <= 80 ? merged : current;
    }
}
