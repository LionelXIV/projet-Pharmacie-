using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Pharmacie.Data;
using Pharmacie.Models;

namespace Pharmacie.Services;

public class PurchaseService
{
    private readonly ApplicationDbContext _db;
    private readonly InventoryService _inventory;

    public PurchaseService(ApplicationDbContext db, InventoryService inventory)
    {
        _db = db;
        _inventory = inventory;
    }

    public async Task<(bool Ok, string? Error)> CreateOrderAsync(
        int supplierId,
        DateTime orderDate,
        string? notes,
        IReadOnlyList<(int ProductId, int Quantity)> lines,
        bool asDraft = false)
    {
        var validLines = lines.Where(l => l.ProductId > 0 && l.Quantity > 0).ToList();
        if (validLines.Count == 0)
            return (false, "Ajoutez au moins une ligne avec un produit et une quantité.");

        var supplierExists = await _db.Suppliers.AnyAsync(s => s.Id == supplierId);
        if (!supplierExists)
            return (false, "Fournisseur introuvable.");

        foreach (var (productId, _) in validLines)
        {
            if (!await _db.Products.AnyAsync(p => p.Id == productId))
                return (false, $"Produit #{productId} introuvable.");
        }

        var order = new PurchaseOrder
        {
            SupplierId = supplierId,
            OrderDate = orderDate.Date,
            Notes = notes,
            Status = asDraft ? PurchaseOrderStatus.Brouillon : PurchaseOrderStatus.Envoyee
        };
        foreach (var (productId, qty) in validLines)
        {
            order.Lines.Add(new PurchaseOrderLine
            {
                ProductId = productId,
                QuantityOrdered = qty,
                QuantityReceived = 0
            });
        }

        _db.PurchaseOrders.Add(order);
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> CancelOrderAsync(int purchaseOrderId)
    {
        var order = await _db.PurchaseOrders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == purchaseOrderId);
        if (order == null)
            return (false, "Commande introuvable.");
        if (order.Status == PurchaseOrderStatus.Annulee)
            return (false, "Commande déjà annulée.");
        if (order.Status == PurchaseOrderStatus.Recue)
            return (false, "Impossible d’annuler une commande entièrement reçue.");
        if (order.Lines.Any(l => l.QuantityReceived > 0))
            return (false, "Impossible d’annuler : une réception a déjà eu lieu.");

        order.Status = PurchaseOrderStatus.Annulee;
        await _db.SaveChangesAsync();
        return (true, null);
    }

    public static void RefreshOrderStatus(PurchaseOrder order)
    {
        if (order.Lines.Count == 0)
            return;
        if (order.Lines.All(l => l.QuantityReceived >= l.QuantityOrdered))
            order.Status = PurchaseOrderStatus.Recue;
        else if (order.Lines.Any(l => l.QuantityReceived > 0))
            order.Status = PurchaseOrderStatus.PartiellementRecue;
        else
            order.Status = PurchaseOrderStatus.Envoyee;
    }

    public async Task<(bool Ok, string? Error)> RecordReceptionAsync(
        int purchaseOrderId,
        ReceptionFormViewModel vm,
        string? userId)
    {
        var order = await _db.PurchaseOrders
            .Include(o => o.Lines)
            .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(o => o.Id == purchaseOrderId);
        if (order == null)
            return (false, "Commande introuvable.");
        if (order.Status == PurchaseOrderStatus.Brouillon)
            throw new InvalidOperationException(
                $"La commande #{order.Id} est en brouillon " +
                "et ne peut pas être réceptionnée. " +
                "Envoyez-la d'abord au fournisseur.");
        if (order.Status == PurchaseOrderStatus.Annulee)
            return (false, "Commande annulée.");
        if (order.Status == PurchaseOrderStatus.Recue)
            return (false, "Commande déjà entièrement reçue.");

        if (vm.Lines.All(l => l.QuantityReceived <= 0))
            return (false, "Indiquez au moins une quantité reçue.");

        var lineById = order.Lines.ToDictionary(l => l.Id);

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var receipt = new GoodsReceipt
            {
                PurchaseOrderId = order.Id,
                ReceivedAt = vm.ReceivedAt,
                Notes = vm.Notes
            };
            _db.GoodsReceipts.Add(receipt);

            foreach (var row in vm.Lines)
            {
                if (row.QuantityReceived <= 0)
                    continue;

                if (!lineById.TryGetValue(row.PurchaseOrderLineId, out var poLine))
                    return await FailAsync(tx, "Ligne de commande invalide.");

                var remaining = poLine.QuantityOrdered - poLine.QuantityReceived;
                if (row.QuantityReceived > remaining)
                    return await FailAsync(tx,
                        $"Quantité trop élevée pour « {poLine.Product?.CommercialName} » (reste {remaining}).");

                if (string.IsNullOrWhiteSpace(row.LotNumber))
                    return await FailAsync(tx,
                        $"Indiquez un n° de lot pour « {poLine.Product?.CommercialName} ».");
                if (!row.ExpirationDate.HasValue)
                    return await FailAsync(tx,
                        $"Indiquez une date d’expiration pour « {poLine.Product?.CommercialName} ».");

                var reason = $"Réception commande #{order.Id}";
                var (ok, err) = await _inventory.StageEntreeAsync(
                    poLine.ProductId,
                    row.LotNumber!,
                    row.ExpirationDate.Value,
                    row.QuantityReceived,
                    reason,
                    userId);
                if (!ok)
                    return await FailAsync(tx, err ?? "Entrée stock impossible.");

                poLine.QuantityReceived += row.QuantityReceived;
                _db.GoodsReceiptLines.Add(new GoodsReceiptLine
                {
                    GoodsReceipt = receipt,
                    PurchaseOrderLineId = poLine.Id,
                    QuantityReceived = row.QuantityReceived,
                    LotNumber = row.LotNumber!.Trim(),
                    ExpirationDate = row.ExpirationDate.Value.Date
                });
            }

            RefreshOrderStatus(order);
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

    private static async Task<(bool Ok, string? Error)> FailAsync(
        IDbContextTransaction tx,
        string message)
    {
        await tx.RollbackAsync();
        return (false, message);
    }
}
