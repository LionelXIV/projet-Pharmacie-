using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Services;
using Pharmacie.Tests.Support;
using Xunit;

namespace Pharmacie.Tests;

/// <summary>
/// Tests du service de vente : stock non expiré, refus si insuffisant ou lots expirés seuls,
/// consommation FIFO par date de péremption (plus proche en premier).
/// </summary>
public class SaleServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _db;
    private readonly SaleService _saleService;

    public SaleServiceTests()
    {
        (_db, _connection) = TestDbContextFactory.Create();
        _saleService = new SaleService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static DateTime SaleDate => new(2026, 6, 15, 10, 0, 0, DateTimeKind.Unspecified);

    /// <summary>
    /// Si la quantité demandée est couverte par des lots non expirés, la vente est acceptée
    /// et le stock produit + les lots diminuent correctement.
    /// </summary>
    [Fact]
    public async Task RecordSale_accepts_when_non_expired_stock_is_sufficient()
    {
        var (_, _, product) = TestCatalogSeed.SeedBasicCatalog(_db);
        product.StockQuantity = 10;
        _db.ProductBatches.Add(new ProductBatch
        {
            ProductId = product.Id,
            LotNumber = "LOT-A",
            ExpirationDate = new DateTime(2027, 1, 1),
            Quantity = 10
        });
        await _db.SaveChangesAsync();

        var (ok, error, saleId) = await _saleService.RecordSaleAsync(
            SaleDate,
            null,
            [(product.Id, 4)],
            userId: "test-user");

        Assert.True(ok, error);
        Assert.Null(error);
        Assert.NotNull(saleId);

        await _db.Entry(product).ReloadAsync();
        Assert.Equal(6, product.StockQuantity);

        var batch = await _db.ProductBatches.SingleAsync(b => b.LotNumber == "LOT-A");
        Assert.Equal(6, batch.Quantity);

        var movements = await _db.StockMovements.Where(m => m.SaleId == saleId).ToListAsync();
        Assert.Single(movements);
        Assert.Equal(StockMovementType.Sortie, movements[0].Type);
        Assert.Equal(4, movements[0].Quantity);
    }

    /// <summary>
    /// Si la quantité sur lots non expirés est inférieure à la demande, la vente est refusée
    /// (aucune persistance partielle grâce à la transaction).
    /// </summary>
    [Fact]
    public async Task RecordSale_rejects_when_non_expired_stock_is_insufficient()
    {
        var (_, _, product) = TestCatalogSeed.SeedBasicCatalog(_db);
        product.StockQuantity = 3;
        _db.ProductBatches.Add(new ProductBatch
        {
            ProductId = product.Id,
            LotNumber = "LOT-B",
            ExpirationDate = new DateTime(2027, 6, 1),
            Quantity = 3
        });
        await _db.SaveChangesAsync();

        var (ok, error, saleId) = await _saleService.RecordSaleAsync(
            SaleDate,
            null,
            [(product.Id, 5)],
            userId: null);

        Assert.False(ok);
        Assert.Null(saleId);
        Assert.NotNull(error);
        Assert.Contains("Stock insuffisant", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("5", error);

        await _db.Entry(product).ReloadAsync();
        Assert.Equal(3, product.StockQuantity);
        Assert.False(await _db.Sales.AnyAsync());
    }

    /// <summary>
    /// Des unités sur des lots déjà expirés à la date de vente ne comptent pas dans le stock vendable :
    /// la vente échoue même si la somme physique (expiré + non expiré) serait suffisante.
    /// </summary>
    [Fact]
    public async Task RecordSale_rejects_when_only_expired_lots_have_stock()
    {
        var (_, _, product) = TestCatalogSeed.SeedBasicCatalog(_db);
        product.StockQuantity = 50;
        _db.ProductBatches.Add(new ProductBatch
        {
            ProductId = product.Id,
            LotNumber = "LOT-EXP",
            ExpirationDate = new DateTime(2020, 1, 1),
            Quantity = 50
        });
        await _db.SaveChangesAsync();

        var (ok, error, saleId) = await _saleService.RecordSaleAsync(
            SaleDate,
            null,
            [(product.Id, 1)],
            userId: null);

        Assert.False(ok);
        Assert.Null(saleId);
        Assert.NotNull(error);
        Assert.Contains("Stock insuffisant", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expir", error, StringComparison.OrdinalIgnoreCase);

        await _db.Entry(product).ReloadAsync();
        Assert.Equal(50, product.StockQuantity);
    }

    /// <summary>
    /// FIFO par péremption : les lots dont la date d’expiration est la plus proche (la plus ancienne)
    /// sont consommés en premier ; ici deux lots non expirés à la date de vente, la vente vide d’abord juillet puis décembre.
    /// </summary>
    [Fact]
    public async Task RecordSale_fifo_consumes_earliest_expiration_first()
    {
        var (_, _, product) = TestCatalogSeed.SeedBasicCatalog(_db);
        product.StockQuantity = 40;
        var lotDec = new ProductBatch
        {
            ProductId = product.Id,
            LotNumber = "LOT-DEC",
            ExpirationDate = new DateTime(2026, 12, 1),
            Quantity = 20
        };
        var lotSooner = new ProductBatch
        {
            ProductId = product.Id,
            LotNumber = "LOT-JUL",
            ExpirationDate = new DateTime(2026, 7, 1),
            Quantity = 20
        };
        _db.ProductBatches.AddRange(lotDec, lotSooner);
        await _db.SaveChangesAsync();

        var (ok, error, saleId) = await _saleService.RecordSaleAsync(
            SaleDate,
            null,
            [(product.Id, 25)],
            userId: null);

        Assert.True(ok, error);
        Assert.NotNull(saleId);

        await _db.Entry(product).ReloadAsync();
        Assert.Equal(15, product.StockQuantity);

        var jul = await _db.ProductBatches.SingleAsync(b => b.LotNumber == "LOT-JUL");
        var dec = await _db.ProductBatches.SingleAsync(b => b.LotNumber == "LOT-DEC");
        Assert.Equal(0, jul.Quantity);
        Assert.Equal(15, dec.Quantity);

        var movements = await _db.StockMovements
            .Where(m => m.SaleId == saleId)
            .OrderBy(m => m.Id)
            .ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Equal(20, movements[0].Quantity);
        Assert.Equal(jul.Id, movements[0].BatchId);
        Assert.Equal(5, movements[1].Quantity);
        Assert.Equal(dec.Id, movements[1].BatchId);
    }
}
