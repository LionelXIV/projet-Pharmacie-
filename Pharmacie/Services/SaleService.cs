using Microsoft.EntityFrameworkCore;
using Pharmacie.Data;
using Pharmacie.Models;

namespace Pharmacie.Services;

public class SaleService
{
    private readonly ApplicationDbContext _db;

    public SaleService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Enregistre une vente : lignes de ticket, sorties stock par lots (FIFO par date de péremption).
    /// Si le produit est une unité (enfant) et que le stock est insuffisant, ouvre automatiquement
    /// le nombre de boîtes parent nécessaires (mouvements tracés « Ouverture boîte »).
    /// </summary>
    public async Task<(bool Ok, string? Error, int? SaleId)> RecordSaleAsync(
        DateTime soldAt,
        string? notes,
        IReadOnlyList<(int ProductId, int Quantity)> lines,
        string? userId,
        PaymentMethod paymentMethod = PaymentMethod.Especes)
    {
        if (lines.Count == 0)
            return (false, "Ajoutez au moins une ligne avec un produit et une quantité.", null);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var refDate = soldAt.Date;

            var sale = new Sale
            {
                SoldAt = soldAt,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                UserId = userId,
                PaymentMethod = paymentMethod
            };
            _db.Sales.Add(sale);

            foreach (var (productId, quantity) in lines)
            {
                var product = await _db.Products.FindAsync(productId);
                if (product == null || !product.IsActive)
                    return await RollbackAsync(tx, "Un produit est introuvable ou inactif.");

                var availableNonExpired = await SumAvailableAsync(productId, refDate);

                // Produit unité : ouvrir des boîtes parent si stock insuffisant
                if (quantity > availableNonExpired
                    && product.ParentProductId.HasValue
                    && product.NbUnitesParBoite is > 0)
                {
                    var unitsNeeded = quantity - availableNonExpired;
                    var (opened, openError) = await TryOpenParentBoxesAsync(
                        product,
                        unitsNeeded,
                        refDate,
                        soldAt,
                        userId);

                    if (!string.IsNullOrEmpty(openError))
                        return await RollbackAsync(tx, openError);

                    if (opened)
                    {
                        // Persister les lots ouverts dans la transaction pour que
                        // les requêtes suivantes (stock / FIFO) voient les tablettes.
                        await _db.SaveChangesAsync();
                        availableNonExpired = await SumAvailableAsync(productId, refDate);
                    }
                }

                if (quantity > availableNonExpired)
                {
                    var onExpiredLots = await _db.ProductBatches
                        .Where(b => b.ProductId == productId && b.Quantity > 0 && b.ExpirationDate.Date < refDate)
                        .SumAsync(b => b.Quantity);

                    var extra = onExpiredLots > 0
                        ? $" ({onExpiredLots} unité(s) encore présent(es) sur des lots expirés ne sont pas vendables à cette date.)"
                        : "";
                    return await RollbackAsync(tx,
                        $"Stock insuffisant (lots non expirés) pour « {product.CommercialName} » : {availableNonExpired} disponible(s), {quantity} demandée(s).{extra}");
                }

                sale.Lines.Add(new SaleLine
                {
                    ProductId = productId,
                    Quantity = quantity,
                    UnitPrice = product.SalePrice
                });

                var remaining = quantity;
                var batches = await _db.ProductBatches
                    .Where(b => b.ProductId == productId && b.Quantity > 0 && b.ExpirationDate.Date >= refDate)
                    .OrderBy(b => b.ExpirationDate)
                    .ThenBy(b => b.Id)
                    .ToListAsync();

                foreach (var batch in batches)
                {
                    if (remaining <= 0)
                        break;

                    var take = Math.Min(batch.Quantity, remaining);
                    batch.Quantity -= take;
                    product.StockQuantity -= take;
                    remaining -= take;

                    _db.StockMovements.Add(new StockMovement
                    {
                        ProductId = productId,
                        BatchId = batch.Id,
                        Type = StockMovementType.Sortie,
                        Quantity = take,
                        Reason = "Vente",
                        OccurredAt = soldAt,
                        UserId = userId,
                        Sale = sale
                    });
                }

                if (remaining > 0)
                    return await RollbackAsync(tx,
                        $"Impossible d'allouer les lots pour « {product.CommercialName} » (écart inattendu — contactez un administrateur).");

                if (product.StockQuantity < 0)
                    product.StockQuantity = 0;
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return (true, null, sale.Id);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private async Task<int> SumAvailableAsync(int productId, DateTime refDate)
    {
        return await _db.ProductBatches
            .Where(b => b.ProductId == productId && b.Quantity > 0 && b.ExpirationDate.Date >= refDate)
            .SumAsync(b => b.Quantity);
    }

    /// <summary>
    /// Ouvre le nombre de boîtes parent nécessaires pour couvrir <paramref name="unitsNeeded"/> unités.
    /// </summary>
    private async Task<(bool Opened, string? Error)> TryOpenParentBoxesAsync(
        Product unitProduct,
        int unitsNeeded,
        DateTime refDate,
        DateTime soldAt,
        string? userId)
    {
        if (unitsNeeded <= 0 || unitProduct.NbUnitesParBoite is not > 0)
            return (false, null);

        var parentId = unitProduct.ParentProductId!.Value;
        var parent = await _db.Products.FirstOrDefaultAsync(p => p.Id == parentId);
        if (parent == null || !parent.IsActive)
            return (false, $"Produit boîte introuvable pour « {unitProduct.CommercialName} ».");

        var nbParBoite = unitProduct.NbUnitesParBoite.Value;
        var boitesAOuvrir = (int)Math.Ceiling(unitsNeeded / (decimal)nbParBoite);

        var parentStock = await SumAvailableAsync(parentId, refDate);
        if (parentStock < boitesAOuvrir)
        {
            var stockDispoEnfant = await SumAvailableAsync(unitProduct.Id, refDate);
            return (false,
                $"Stock insuffisant pour « {unitProduct.CommercialName} ». " +
                $"Tablettes disponibles : {stockDispoEnfant}. " +
                $"Boîtes disponibles : {parentStock}. " +
                $"Impossible de satisfaire la demande de {stockDispoEnfant + unitsNeeded} tablettes " +
                $"({unitsNeeded} manquante(s) → {boitesAOuvrir} boîte(s) requise(s)).");
        }

        var remainingToOpen = boitesAOuvrir;
        var parentBatches = await _db.ProductBatches
            .Where(b => b.ProductId == parentId && b.Quantity > 0 && b.ExpirationDate.Date >= refDate)
            .OrderBy(b => b.ExpirationDate)
            .ThenBy(b => b.Id)
            .ToListAsync();

        foreach (var parentBatch in parentBatches)
        {
            if (remainingToOpen <= 0)
                break;

            var boitesFromThisBatch = Math.Min(parentBatch.Quantity, remainingToOpen);
            parentBatch.Quantity -= boitesFromThisBatch;
            parent.StockQuantity -= boitesFromThisBatch;
            remainingToOpen -= boitesFromThisBatch;

            var unitesOuvertes = boitesFromThisBatch * nbParBoite;

            var enfantBatch = await _db.ProductBatches
                .FirstOrDefaultAsync(b =>
                    b.ProductId == unitProduct.Id
                    && b.LotNumber == parentBatch.LotNumber
                    && b.ExpirationDate.Date == parentBatch.ExpirationDate.Date);

            if (enfantBatch == null)
            {
                enfantBatch = new ProductBatch
                {
                    ProductId = unitProduct.Id,
                    LotNumber = parentBatch.LotNumber,
                    ExpirationDate = parentBatch.ExpirationDate.Date,
                    Quantity = unitesOuvertes
                };
                _db.ProductBatches.Add(enfantBatch);
            }
            else
            {
                enfantBatch.Quantity += unitesOuvertes;
            }

            unitProduct.StockQuantity += unitesOuvertes;

            _db.StockMovements.Add(new StockMovement
            {
                ProductId = parent.Id,
                BatchId = parentBatch.Id,
                Type = StockMovementType.Sortie,
                Quantity = boitesFromThisBatch,
                Reason =
                    $"Ouverture boîte → {unitesOuvertes} unités pour {unitProduct.CommercialName}",
                OccurredAt = soldAt,
                UserId = userId
            });

            _db.StockMovements.Add(new StockMovement
            {
                ProductId = unitProduct.Id,
                Batch = enfantBatch,
                Type = StockMovementType.Entree,
                Quantity = unitesOuvertes,
                Reason =
                    $"Ouverture boîte ← {boitesFromThisBatch} boîte(s) de {parent.CommercialName}",
                OccurredAt = soldAt,
                UserId = userId
            });
        }

        if (remainingToOpen > 0)
            return (false, $"Impossible d'ouvrir assez de boîtes pour « {unitProduct.CommercialName} ».");

        if (parent.StockQuantity < 0)
            parent.StockQuantity = 0;

        return (true, null);
    }

    private static async Task<(bool Ok, string? Error, int? SaleId)> RollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx,
        string message)
    {
        await tx.RollbackAsync();
        return (false, message, null);
    }
}
