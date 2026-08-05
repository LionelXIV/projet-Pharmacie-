using Microsoft.EntityFrameworkCore;
using Pharmacie.Data;
using Pharmacie.Models;

namespace Pharmacie.Services;

public class AvoirService
{
    private readonly ApplicationDbContext _db;

    public AvoirService(ApplicationDbContext db)
    {
        _db = db;
    }

    private async Task<string> GenerateNumeroAsync()
    {
        var year = DateTime.Now.Year;
        var count = await _db.Avoirs
            .Where(a => a.DateCreation.Year == year)
            .CountAsync();
        return $"AV-{year}-{(count + 1):D3}";
    }

    /// <summary>
    /// Crée un avoir : paiement immédiat, stock produit peut devenir négatif.
    /// Un mouvement Sortie est toujours créé.
    /// </summary>
    public async Task<(bool Success, string Error, int AvoirId)> CreateAvoirAsync(
        string clientNom,
        string? clientTel,
        string? numeroIdentite,
        List<(int ProductId, int Quantity)> lignes,
        PaymentMethod paymentMethod,
        string userId,
        int? vendeurId,
        string? notes)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(clientNom))
                return await FailAsync(tx, "Le nom du client est obligatoire.");

            if (lignes == null || !lignes.Any(l => l.ProductId > 0 && l.Quantity > 0))
                return await FailAsync(tx, "Ajoutez au moins un produit.");

            var numero = await GenerateNumeroAsync();
            var now = DateTime.Now;
            var refDate = now.Date;
            var client = clientNom.Trim();

            var avoir = new Avoir
            {
                Numero = numero,
                ClientNom = client,
                ClientTelephone = string.IsNullOrWhiteSpace(clientTel) ? null : clientTel.Trim(),
                NumeroIdentite = string.IsNullOrWhiteSpace(numeroIdentite) ? null : numeroIdentite.Trim(),
                DateCreation = now,
                PaymentMethod = paymentMethod,
                CreatedByUserId = userId,
                VendeurId = vendeurId,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                Statut = AvoirStatut.EnAttente
            };

            decimal total = 0;

            foreach (var (productId, qty) in lignes.Where(l => l.ProductId > 0 && l.Quantity > 0))
            {
                var product = await _db.Products
                    .Include(p => p.Batches)
                    .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);

                if (product == null)
                    return await FailAsync(tx, $"Produit #{productId} introuvable ou inactif.");

                var ligne = new AvoirLigne
                {
                    ProductId = productId,
                    Quantity = qty,
                    UnitPrice = product.SalePrice,
                    EstLivre = false
                };
                avoir.Lignes.Add(ligne);
                total += ligne.UnitPrice * ligne.Quantity;

                // Stock produit : peut devenir négatif
                product.StockQuantity -= qty;

                // Décrémenter les lots disponibles (sans passer sous 0 sur le lot)
                var remaining = qty;
                ProductBatch? batchForMovement = null;

                var lots = product.Batches
                    .Where(b => b.Quantity > 0 && b.ExpirationDate.Date >= refDate)
                    .OrderBy(b => b.ExpirationDate)
                    .ThenBy(b => b.Id)
                    .ToList();

                foreach (var lot in lots)
                {
                    if (remaining <= 0) break;
                    var taken = Math.Min(lot.Quantity, remaining);
                    lot.Quantity -= taken;
                    remaining -= taken;
                    batchForMovement ??= lot;
                }

                // Si aucun lot avec stock : rattacher le mouvement à un lot existant ou en créer un
                if (batchForMovement == null)
                {
                    batchForMovement = product.Batches
                        .OrderByDescending(b => b.Id)
                        .FirstOrDefault();

                    if (batchForMovement == null)
                    {
                        batchForMovement = new ProductBatch
                        {
                            ProductId = productId,
                            LotNumber = $"AVOIR-{numero}",
                            ExpirationDate = refDate.AddYears(5),
                            Quantity = 0
                        };
                        product.Batches.Add(batchForMovement);
                    }
                }

                _db.StockMovements.Add(new StockMovement
                {
                    ProductId = productId,
                    Batch = batchForMovement,
                    Type = StockMovementType.Sortie,
                    Quantity = qty,
                    OccurredAt = now,
                    UserId = userId,
                    Reason = $"Avoir {numero} — {client}"
                });
            }

            avoir.MontantTotal = total;
            _db.Avoirs.Add(avoir);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, "", avoir.Id);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return (false, ex.Message, 0);
        }
    }

    public async Task<(bool Success, string Error)> MarquerLivreAsync(int avoirId, int ligneId)
    {
        var avoir = await _db.Avoirs
            .Include(a => a.Lignes)
            .FirstOrDefaultAsync(a => a.Id == avoirId);

        if (avoir == null)
            return (false, "Avoir introuvable.");

        if (avoir.Statut == AvoirStatut.Annule)
            return (false, "Cet avoir est annulé.");

        var ligne = avoir.Lignes.FirstOrDefault(l => l.Id == ligneId);
        if (ligne == null)
            return (false, "Ligne introuvable.");

        if (ligne.EstLivre)
            return (false, "Cette ligne est déjà livrée.");

        ligne.EstLivre = true;
        ligne.DateLivraison = DateTime.Now;

        if (avoir.Lignes.All(l => l.EstLivre))
            avoir.Statut = AvoirStatut.Livre;

        await _db.SaveChangesAsync();
        return (true, "");
    }

    public async Task<(bool Success, string Error)> AnnulerAvoirAsync(int avoirId)
    {
        var avoir = await _db.Avoirs.FirstOrDefaultAsync(a => a.Id == avoirId);
        if (avoir == null)
            return (false, "Avoir introuvable.");

        if (avoir.Statut == AvoirStatut.Livre)
            return (false, "Impossible d'annuler un avoir déjà livré.");

        if (avoir.Statut == AvoirStatut.Annule)
            return (false, "Cet avoir est déjà annulé.");

        avoir.Statut = AvoirStatut.Annule;
        await _db.SaveChangesAsync();
        return (true, "");
    }

    private static async Task<(bool Success, string Error, int AvoirId)> FailAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, string error)
    {
        await tx.RollbackAsync();
        return (false, error, 0);
    }
}
