using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Services;
using Pharmacie.Tests.Support;
using Xunit;

namespace Pharmacie.Tests;

/// <summary>
/// Tests de réception de commande : après <see cref="PurchaseService.RecordReceptionAsync"/>,
/// le stock produit augmente, une ligne de commande est mise à jour et un lot est créé.
/// </summary>
public class PurchaseServiceReceptionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _db;
    private readonly PurchaseService _purchaseService;

    public PurchaseServiceReceptionTests()
    {
        (_db, _connection) = TestDbContextFactory.Create();
        var inventory = new InventoryService(_db);
        _purchaseService = new PurchaseService(_db, inventory);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// Une réception partielle avec n° de lot et date d’expiration met à jour le stock du produit,
    /// crée un lot et augmente QuantityReceived sur la ligne de commande.
    /// </summary>
    [Fact]
    public async Task RecordReception_updates_product_stock_and_order_line()
    {
        var (_, supplier, product) = TestCatalogSeed.SeedBasicCatalog(_db);
        product.StockQuantity = 0;
        await _db.SaveChangesAsync();

        var (orderOk, orderErr) = await _purchaseService.CreateOrderAsync(
            supplier.Id,
            new DateTime(2026, 5, 1),
            notes: null,
            [(product.Id, 10)]);
        Assert.True(orderOk, orderErr);

        var order = await _db.PurchaseOrders.Include(o => o.Lines).SingleAsync();
        var line = order.Lines.Single();

        var vm = new ReceptionFormViewModel
        {
            PurchaseOrderId = order.Id,
            ReceivedAt = new DateTime(2026, 5, 10, 14, 0, 0),
            Notes = "Test réception",
            Lines =
            [
                new ReceptionLineRowViewModel
                {
                    PurchaseOrderLineId = line.Id,
                    ProductName = product.CommercialName,
                    QuantityOrdered = 10,
                    QuantityReceivedBefore = 0,
                    QuantityReceived = 4,
                    LotNumber = "RCPT-001",
                    ExpirationDate = new DateTime(2028, 1, 1)
                }
            ]
        };

        var (ok, err) = await _purchaseService.RecordReceptionAsync(order.Id, vm, userId: "receiver-1");
        Assert.True(ok, err);
        Assert.Null(err);

        await _db.Entry(product).ReloadAsync();
        Assert.Equal(4, product.StockQuantity);

        await _db.Entry(line).ReloadAsync();
        Assert.Equal(4, line.QuantityReceived);

        var batch = await _db.ProductBatches.SingleAsync(b => b.LotNumber == "RCPT-001");
        Assert.Equal(4, batch.Quantity);
        Assert.Equal(product.Id, batch.ProductId);

        Assert.Single(await _db.GoodsReceipts.Where(r => r.PurchaseOrderId == order.Id).ToListAsync());
        Assert.True(await _db.StockMovements.AnyAsync(m => m.Type == StockMovementType.Entree && m.Quantity == 4));
    }
}
