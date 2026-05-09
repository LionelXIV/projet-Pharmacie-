using Microsoft.EntityFrameworkCore;
using Pharmacie.Data;
using Pharmacie.Models;

namespace Pharmacie.Services;

public class InventoryService
{
    private readonly ApplicationDbContext _db;

    public InventoryService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Prépare entrée stock (lot + mouvement + produit) sans SaveChanges — à utiliser dans une transaction globale.
    /// </summary>
    public async Task<(bool Ok, string? Error)> StageEntreeAsync(
        int productId,
        string lotNumber,
        DateTime expirationDate,
        int quantity,
        string? reason,
        string? userId)
    {
        if (quantity <= 0)
            return (false, "La quantité doit être positive.");

        var product = await _db.Products.FindAsync(productId);
        if (product == null)
            return (false, "Produit introuvable.");

        var batch = new ProductBatch
        {
            ProductId = productId,
            LotNumber = lotNumber.Trim(),
            ExpirationDate = expirationDate.Date,
            Quantity = quantity
        };
        _db.ProductBatches.Add(batch);
        _db.StockMovements.Add(new StockMovement
        {
            ProductId = productId,
            Batch = batch,
            Type = StockMovementType.Entree,
            Quantity = quantity,
            Reason = reason,
            OccurredAt = DateTime.Now,
            UserId = userId
        });
        product.StockQuantity += quantity;
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> RecordEntreeAsync(
        int productId,
        string lotNumber,
        DateTime expirationDate,
        int quantity,
        string? reason,
        string? userId)
    {
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var (ok, err) = await StageEntreeAsync(productId, lotNumber, expirationDate, quantity, reason, userId);
            if (!ok)
            {
                await tx.RollbackAsync();
                return (false, err);
            }

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return (true, null);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> RecordSortieAsync(
        int batchId,
        int quantity,
        string? reason,
        string? userId)
    {
        if (quantity <= 0)
            return (false, "La quantité doit être positive.");

        var batch = await _db.ProductBatches
            .Include(b => b.Product)
            .FirstOrDefaultAsync(b => b.Id == batchId);
        if (batch?.Product == null)
            return (false, "Lot introuvable.");

        if (quantity > batch.Quantity)
            return (false, "Quantité indisponible sur ce lot.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            batch.Quantity -= quantity;
            batch.Product.StockQuantity -= quantity;
            _db.StockMovements.Add(new StockMovement
            {
                ProductId = batch.ProductId,
                BatchId = batch.Id,
                Type = StockMovementType.Sortie,
                Quantity = quantity,
                Reason = reason,
                OccurredAt = DateTime.Now,
                UserId = userId
            });
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return (true, null);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<(bool Ok, string? Error)> RecordAjustementAsync(
        int batchId,
        int delta,
        string? reason,
        string? userId)
    {
        if (delta == 0)
            return (false, "La variation doit être différente de zéro.");

        var batch = await _db.ProductBatches
            .Include(b => b.Product)
            .FirstOrDefaultAsync(b => b.Id == batchId);
        if (batch?.Product == null)
            return (false, "Lot introuvable.");

        var newQty = batch.Quantity + delta;
        if (newQty < 0)
            return (false, "Le lot ne peut pas avoir une quantité négative.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            batch.Quantity = newQty;
            batch.Product.StockQuantity += delta;
            _db.StockMovements.Add(new StockMovement
            {
                ProductId = batch.ProductId,
                BatchId = batch.Id,
                Type = StockMovementType.Ajustement,
                Quantity = delta,
                Reason = reason,
                OccurredAt = DateTime.Now,
                UserId = userId
            });
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
            return (true, null);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
