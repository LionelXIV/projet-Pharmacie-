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

                var availableNonExpired = await _db.ProductBatches
                    .Where(b => b.ProductId == productId && b.Quantity > 0 && b.ExpirationDate.Date >= refDate)
                    .SumAsync(b => b.Quantity);

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

    private static async Task<(bool Ok, string? Error, int? SaleId)> RollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx,
        string message)
    {
        await tx.RollbackAsync();
        return (false, message, null);
    }
}
