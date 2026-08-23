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
                var (ok, err, _) = await _inventory.StageEntreeAsync(
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
                    ProductId = poLine.ProductId,
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

    /// <summary>
    /// Supprime un BL et retire du stock les quantités encore présentes sur les lots de cette réception.
    /// Refusé si une partie a déjà été vendue.
    /// </summary>
    public async Task<(bool Ok, string? Error)> DeleteReceiptAsync(int receiptId, string? userId)
    {
        var receipt = await _db.GoodsReceipts
            .Include(r => r.Lines)
            .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(r => r.Id == receiptId);
        if (receipt == null)
            return (false, "Bon de livraison introuvable.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var reasonTag = receipt.PurchaseOrderId is int poId
                ? $"Réception commande #{poId}"
                : $"BL Direct #{receipt.Id}";
            var reverseReason = $"Suppression BL #{receipt.Id}";

            var assignedBatchIds = new HashSet<int>();

            foreach (var line in receipt.Lines.Where(l => l.ProductId is > 0 && l.QuantityReceived > 0))
            {
                var productId = line.ProductId!.Value;
                var productName = line.Product?.CommercialName ?? $"#{productId}";
                var lot = (line.LotNumber ?? "").Trim();
                var exp = line.ExpirationDate.Date;

                var entrees = await _db.StockMovements
                    .Include(m => m.Batch!)
                    .ThenInclude(b => b.Product)
                    .Where(m => m.Type == StockMovementType.Entree
                                && m.ProductId == productId
                                && m.Batch != null
                                && m.Batch.LotNumber == lot
                                && m.Batch.ExpirationDate.Date == exp
                                && m.Quantity == line.QuantityReceived)
                    .OrderBy(m => m.OccurredAt)
                    .ToListAsync();

                var entree = entrees.FirstOrDefault(m =>
                    !assignedBatchIds.Contains(m.BatchId)
                    && (string.IsNullOrEmpty(m.Reason)
                        || m.Reason.Contains(reasonTag, StringComparison.Ordinal)
                        || m.Reason.Contains($"BL Direct #{receipt.Id}", StringComparison.Ordinal)
                        || m.Reason.Contains($"BL Direct #{receipt.Id}", StringComparison.Ordinal)
                        || (receipt.PurchaseOrderId is int oid
                            && (m.Reason.Contains($"Réception commande #{oid}", StringComparison.Ordinal)
                                || m.Reason.Contains($"Réception commande #{oid}", StringComparison.Ordinal)))));

                if (entree?.Batch?.Product == null)
                {
                    // Lots créés sans motif exploitable : on prend le lot encore inutilisé le plus proche.
                    entree = entrees.FirstOrDefault(m => !assignedBatchIds.Contains(m.BatchId) && m.Batch?.Product != null);
                }

                if (entree?.Batch?.Product == null)
                    return await FailAsync(tx,
                        $"Lots de stock introuvables pour « {productName} » (lot {lot}). Suppression impossible.");

                var batch = entree.Batch;
                assignedBatchIds.Add(batch.Id);

                var soldOnBatch = await _db.StockMovements.AnyAsync(m =>
                    m.BatchId == batch.Id && m.SaleId != null);
                if (soldOnBatch)
                    return await FailAsync(tx,
                        $"Impossible de supprimer le BL : du stock de « {productName} » (lot {lot}) a déjà été vendu.");

                var ouvertures = await _db.StockMovements
                    .Where(m => m.BatchId == batch.Id
                                && m.Type == StockMovementType.Sortie
                                && m.SaleId == null
                                && m.Reason != null
                                && (m.Reason.Contains("Ouverture boîte")
                                    || m.Reason.Contains("Ouverture boîte")
                                    || m.Reason.Contains("Ouverture boîte")))
                    .ToListAsync();
                var qtyOpened = ouvertures.Sum(m => m.Quantity);

                var otherSorties = await _db.StockMovements
                    .Where(m => m.BatchId == batch.Id
                                && m.Type == StockMovementType.Sortie
                                && m.SaleId == null
                                && (m.Reason == null
                                    || (!m.Reason.Contains("Ouverture boîte")
                                        && !m.Reason.Contains("Ouverture boîte")
                                        && !m.Reason.Contains("Ouverture boîte"))))
                    .SumAsync(m => m.Quantity);
                if (otherSorties > 0)
                    return await FailAsync(tx,
                        $"Impossible de supprimer le BL : le lot « {productName} » {lot} a déjà été consommé.");

                var remainingOnBatch = batch.Quantity;
                var expectedRemaining = line.QuantityReceived - qtyOpened;
                if (remainingOnBatch < expectedRemaining)
                    return await FailAsync(tx,
                        $"Impossible de supprimer le BL : le stock restant de « {productName} » est inférieur à la quantité reçue (déjà vendu ou sorti).");

                if (qtyOpened > 0)
                {
                    var child = await _db.Products
                        .Include(p => p.Batches)
                        .FirstOrDefaultAsync(p => p.ParentProductId == productId);
                    if (child == null || child.NbUnitesParBoite is not > 0)
                        return await FailAsync(tx,
                            $"Des boîtes de « {productName} » ont été ouvertes : impossible d’annuler le stock détail.");

                    var tablettes = qtyOpened * child.NbUnitesParBoite.Value;
                    var childBatch = child.Batches.FirstOrDefault(b =>
                        b.LotNumber == lot && b.ExpirationDate.Date == exp);
                    if (childBatch == null)
                        childBatch = await _db.ProductBatches
                            .Include(b => b.Product)
                            .FirstOrDefaultAsync(b =>
                                b.ProductId == child.Id
                                && b.LotNumber == lot
                                && b.ExpirationDate.Date == exp);
                    else
                        childBatch.Product ??= child;

                    if (childBatch?.Product == null)
                        return await FailAsync(tx,
                            $"Lot unité introuvable pour « {productName} ». Suppression refusée pour ne pas fausser le stock.");

                    var childSold = await _db.StockMovements.AnyAsync(m =>
                        m.BatchId == childBatch.Id && m.SaleId != null);
                    if (childSold || childBatch.Quantity < tablettes)
                        return await FailAsync(tx,
                            $"Impossible de supprimer le BL : des unités de « {productName} » ouvertes à la réception ont déjà été vendues.");

                    var (okChild, errChild) = _inventory.StageSortie(
                        childBatch,
                        tablettes,
                        reverseReason + " (unités ouvertes)",
                        userId);
                    if (!okChild)
                        return await FailAsync(tx, errChild ?? "Sortie stock unités impossible.");
                }

                if (remainingOnBatch > 0)
                {
                    var (okParent, errParent) = _inventory.StageSortie(
                        batch,
                        remainingOnBatch,
                        reverseReason,
                        userId);
                    if (!okParent)
                        return await FailAsync(tx, errParent ?? "Sortie stock impossible.");
                }
            }

            if (receipt.PurchaseOrderId is int orderId)
            {
                var order = await _db.PurchaseOrders
                    .Include(o => o.Lines)
                    .FirstOrDefaultAsync(o => o.Id == orderId);
                if (order != null)
                {
                    foreach (var line in receipt.Lines.Where(l => l.PurchaseOrderLineId.HasValue))
                    {
                        var poLine = order.Lines.FirstOrDefault(l => l.Id == line.PurchaseOrderLineId);
                        if (poLine == null)
                            continue;
                        poLine.QuantityReceived = Math.Max(0, poLine.QuantityReceived - line.QuantityReceived);
                    }

                    RefreshOrderStatus(order);
                }
            }

            _db.GoodsReceiptLines.RemoveRange(receipt.Lines);
            _db.GoodsReceipts.Remove(receipt);
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
