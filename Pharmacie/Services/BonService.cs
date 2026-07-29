using Microsoft.EntityFrameworkCore;
using Pharmacie.Data;
using Pharmacie.Models;

namespace Pharmacie.Services;

public class BonService
{
    private readonly ApplicationDbContext _db;

    public BonService(ApplicationDbContext db)
    {
        _db = db;
    }

    private async Task<string> GenerateNumeroAsync()
    {
        var year = DateTime.Now.Year;
        var count = await _db.Bons
            .Where(b => b.DateCreation.Year == year)
            .CountAsync();
        return $"BON-{year}-{(count + 1):D3}";
    }

    /// <summary>
    /// Crée un bon : déstocke immédiatement (FIFO), calcule le total.
    /// </summary>
    public async Task<(bool Success, string Error, int BonId)> CreateBonAsync(
        string clientNom,
        string? clientTel,
        string? notes,
        List<(int ProductId, int Quantity, decimal DiscountPercent, decimal DiscountAmount, string DiscountType)> lignes,
        string userId,
        int? vendeurId)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var numero = await GenerateNumeroAsync();
            var now = DateTime.Now;
            var refDate = now.Date;

            var bon = new Bon
            {
                Numero = numero,
                ClientNom = clientNom.Trim(),
                ClientTelephone = string.IsNullOrWhiteSpace(clientTel) ? null : clientTel.Trim(),
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                DateCreation = now,
                CreatedByUserId = userId,
                VendeurId = vendeurId,
                Statut = BonStatut.Ouvert
            };

            decimal total = 0;

            foreach (var (productId, qty, discPct, discAmt, discType) in lignes)
            {
                var product = await _db.Products
                    .Include(p => p.Batches)
                    .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);

                if (product == null)
                {
                    await tx.RollbackAsync();
                    return (false, $"Produit #{productId} introuvable ou inactif.", 0);
                }

                var stockDispo = product.Batches
                    .Where(b => b.Quantity > 0 && b.ExpirationDate.Date >= refDate)
                    .Sum(b => b.Quantity);

                if (stockDispo < qty)
                {
                    await tx.RollbackAsync();
                    return (false, $"Stock insuffisant pour « {product.CommercialName} ». Disponible : {stockDispo}, demandé : {qty}.", 0);
                }

                var ligne = new BonLigne
                {
                    ProductId = productId,
                    Quantity = qty,
                    UnitPrice = product.SalePrice,
                    DiscountPercent = discPct,
                    DiscountAmount = discAmt,
                    DiscountType = discType
                };
                bon.Lignes.Add(ligne);
                total += ligne.LineTotal;

                // FIFO déstockage
                var lots = product.Batches
                    .Where(b => b.Quantity > 0 && b.ExpirationDate.Date >= refDate)
                    .OrderBy(b => b.ExpirationDate)
                    .ThenBy(b => b.Id)
                    .ToList();

                var remaining = qty;
                foreach (var lot in lots)
                {
                    if (remaining <= 0) break;
                    var taken = Math.Min(lot.Quantity, remaining);
                    lot.Quantity -= taken;
                    product.StockQuantity -= taken;
                    remaining -= taken;

                    _db.StockMovements.Add(new StockMovement
                    {
                        ProductId = productId,
                        BatchId = lot.Id,
                        Type = StockMovementType.Sortie,
                        Quantity = taken,
                        OccurredAt = now,
                        UserId = userId,
                        Reason = $"Bon {numero}"
                    });
                }

                if (product.StockQuantity < 0)
                    product.StockQuantity = 0;
            }

            bon.MontantTotal = total;
            _db.Bons.Add(bon);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, "", bon.Id);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            return (false, ex.Message, 0);
        }
    }

    /// <summary>
    /// Enregistre un règlement (partiel ou total).
    /// </summary>
    public async Task<(bool Success, string Error)> ReglerBonAsync(
        int bonId,
        decimal montant,
        PaymentMethod paymentMethod,
        string userId)
    {
        var bon = await _db.Bons.FirstOrDefaultAsync(b => b.Id == bonId);
        if (bon == null) return (false, "Bon introuvable.");
        if (bon.Statut == BonStatut.Solde) return (false, "Ce bon est déjà soldé.");
        if (bon.Statut == BonStatut.Annule) return (false, "Ce bon est annulé.");
        if (montant <= 0) return (false, "Le montant doit être supérieur à 0.");
        if (montant > bon.MontantRestant)
            return (false, $"Le montant ({montant:N0} FCFA) dépasse le restant dû ({bon.MontantRestant:N0} FCFA).");

        _db.ReglementBons.Add(new ReglementBon
        {
            BonId = bonId,
            DateReglement = DateTime.Now,
            Montant = montant,
            PaymentMethod = paymentMethod,
            EncaisseParUserId = userId
        });

        bon.MontantRegle += montant;
        bon.Statut = bon.MontantRegle >= bon.MontantTotal ? BonStatut.Solde : BonStatut.PartiellemntRegle;

        await _db.SaveChangesAsync();
        return (true, "");
    }
}
