using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Services;
using Pharmacie.Tests.Support;
using Xunit;

namespace Pharmacie.Tests;

/// <summary>
/// Tests d'orchestration d'import : préparation, confirmation, création catalogue/stock.
/// </summary>
public class ProductImportServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _db;
    private readonly ProductImportService _importService;
    private readonly string _userId;
    private readonly List<(ImportLine Line, ImportLineAction Action)> _pendingImportLines = new();

    public ProductImportServiceTests()
    {
        (_db, _connection) = TestDbContextFactory.Create();
        _userId = TestIdentitySeed.EnsureUserAsync(_db).GetAwaiter().GetResult();
        _importService = new ProductImportService(
            _db,
            new ExcelReaderService(),
            new ImportValidationService(),
            new ImportMatchingService(_db),
            new InventoryService(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task PrepareImportAsync_creates_expected_import_lines_and_anomalies()
    {
        await using var stream = ImportTestExcelBuilder.CreateWorkbook(ws =>
        {
            ImportTestExcelBuilder.WriteRow(ws, 2, "VAL001", "Ligne valide", 5, 3m, 10m);
            ImportTestExcelBuilder.WriteRow(ws, 3, "DUP001", "Même libellé", 2, 3m, 10m);
            ImportTestExcelBuilder.WriteRow(ws, 4, "DUP001", "Même libellé", 1, 3m, 10m);
            ImportTestExcelBuilder.WriteRow(ws, 5, "ZERO001", "PPH zéro", 1, 3m, 0m);
        });

        var batchId = await _importService.PrepareImportAsync(stream, "prepare-test.xlsx", _userId);

        var lineCount = await _db.ImportLines.CountAsync(l => l.ImportBatchId == batchId);
        Assert.Equal(4, lineCount);

        var anomalyCount = await _db.ImportAnomalies.CountAsync(a => a.ImportLine!.ImportBatchId == batchId);
        Assert.Equal(3, anomalyCount);

        var blockingCount = await _db.ImportAnomalies.CountAsync(a =>
            a.ImportLine!.ImportBatchId == batchId
            && a.Severity == ImportAnomalySeverity.Bloquante);
        Assert.Equal(1, blockingCount);

        var duplicateWarnings = await _db.ImportAnomalies.CountAsync(a =>
            a.ImportLine!.ImportBatchId == batchId
            && a.AnomalyType == ImportAnomalyType.CipDupliqueDansLeFichier);
        Assert.Equal(2, duplicateWarnings);
    }

    [Fact]
    public async Task PrepareImportAsync_persists_creation_produit_resolved_action()
    {
        await using var stream = ImportTestExcelBuilder.CreateWorkbook(ws =>
            ImportTestExcelBuilder.WriteRow(ws, 2, "NEW-CIP-001", "Nouveau produit test", 5, 3m, 10m));

        var batchId = await _importService.PrepareImportAsync(stream, "creation-action.xlsx", _userId);

        var line = await _db.ImportLines
            .AsNoTracking()
            .SingleAsync(l => l.ImportBatchId == batchId);

        Assert.Equal(ImportLineAction.CreationProduit, line.ResolvedAction);
    }

    [Fact]
    public async Task ConfirmImportAsync_refuses_when_blocking_anomaly_is_unresolved()
    {
        await using var stream = ImportTestExcelBuilder.CreateWorkbook(ws =>
            ImportTestExcelBuilder.WriteRow(ws, 2, "BLOCK001", "PPH nul", 1, 3m, 0m));

        var batchId = await _importService.PrepareImportAsync(stream, "blocking.xlsx", _userId);

        await Assert.ThrowsAsync<ProductImportUnresolvedAnomaliesException>(
            () => _importService.ConfirmImportAsync(batchId, _userId));

        Assert.False(await _db.Products.AnyAsync(p => p.Cip == "BLOCK001"));
        Assert.False(await _db.ProductBatches.AnyAsync());
        Assert.False(await _db.StockMovements.AnyAsync());
    }

    [Fact]
    public async Task ConfirmImportAsync_creates_expected_products_and_batches()
    {
        var batchId = await SeedReadyToConfirmBatchAsync();

        await _importService.ConfirmImportAsync(batchId, _userId);

        Assert.Equal(2, await _db.Products.CountAsync(p => p.Cip == "CONF-NEW1" || p.Cip == "CONF-NEW2"));
        Assert.Equal(3, await _db.ProductBatches.CountAsync());
        Assert.Equal(3, await _db.StockMovements.CountAsync(m => m.Type == StockMovementType.Entree));

        var batch = await _db.ImportBatches.FindAsync(batchId);
        Assert.Equal(ImportBatchStatus.Confirme, batch!.Status);
        Assert.NotNull(batch.ConfirmedAt);
    }

    [Fact]
    public async Task ConfirmImportAsync_creates_all_new_products_with_product_type_inconnu()
    {
        var batchId = await SeedReadyToConfirmBatchAsync();

        await _importService.ConfirmImportAsync(batchId, _userId);

        var created = await _db.Products
            .Where(p => p.Cip == "CONF-NEW1" || p.Cip == "CONF-NEW2")
            .ToListAsync();

        Assert.Equal(2, created.Count);
        Assert.All(created, p => Assert.Equal(ProductType.Inconnu, p.ProductType));
        Assert.DoesNotContain(created, p =>
            p.ProductType is ProductType.Medicament or ProductType.Parapharmacie);
    }

    [Fact]
    public async Task ConfirmImportAsync_never_creates_batch_with_negative_quantity()
    {
        var (_, _, existing) = TestCatalogSeed.SeedBasicCatalog(_db);
        existing.Cip = "NEG-EXIST";
        await _db.SaveChangesAsync();

        var batch = new ImportBatch
        {
            FileName = "negative-qty.xlsx",
            UploadedAt = DateTime.Now,
            Status = ImportBatchStatus.EnAttenteValidation,
            TotalRows = 2
        };
        _db.ImportBatches.Add(batch);
        await _db.SaveChangesAsync();

        AddImportLines(
            CreateImportLine(batch.Id, 2, "NEG-NEW", "Création qté négative", -4, 2m, 5m, ImportLineAction.CreationProduit),
            CreateImportLine(batch.Id, 3, "NEG-EXIST", "Lot qté négative", -2, 2m, 5m, ImportLineAction.NouveauLot, existing.Id));
        await SaveImportLinesAsync();

        await _importService.ConfirmImportAsync(batch.Id, _userId);

        Assert.True(await _db.Products.AnyAsync(p => p.Cip == "NEG-NEW"));
        Assert.Empty(await _db.ProductBatches.ToListAsync());
        Assert.False(await _db.StockMovements.AnyAsync());
    }

    [Fact]
    public async Task ConfirmImportAsync_updates_only_reference_prices_on_existing_product()
    {
        var (_, _, existing) = TestCatalogSeed.SeedBasicCatalog(_db);
        existing.Cip = "PRIX-EXIST";
        existing.SalePrice = 99m;
        existing.PurchasePrice = 88m;
        existing.ReferencePurchasePrice = 1m;
        existing.RegulatedSalePrice = 2m;
        await _db.SaveChangesAsync();

        var batch = new ImportBatch
        {
            FileName = "price-update.xlsx",
            UploadedAt = DateTime.Now,
            Status = ImportBatchStatus.EnAttenteValidation,
            TotalRows = 1
        };
        _db.ImportBatches.Add(batch);
        await _db.SaveChangesAsync();

        AddImportLines(CreateImportLine(
            batch.Id, 2, "PRIX-EXIST", "Mise à jour prix", 0, 12.50m, 24.90m,
            ImportLineAction.MiseAJourPrix, existing.Id));
        await SaveImportLinesAsync();

        await _importService.ConfirmImportAsync(batch.Id, _userId);

        await _db.Entry(existing).ReloadAsync();
        Assert.Equal(12.50m, existing.ReferencePurchasePrice);
        Assert.Equal(24.90m, existing.RegulatedSalePrice);
        Assert.Equal(99m, existing.SalePrice);
        Assert.Equal(88m, existing.PurchasePrice);
    }

    private async Task<int> SeedReadyToConfirmBatchAsync()
    {
        var (_, _, existing) = TestCatalogSeed.SeedBasicCatalog(_db);
        existing.Cip = "CONF-EXIST";
        existing.SalePrice = 99m;
        existing.PurchasePrice = 88m;
        existing.ReferencePurchasePrice = 1m;
        existing.RegulatedSalePrice = 2m;
        await _db.SaveChangesAsync();

        var batch = new ImportBatch
        {
            FileName = "confirm-test.xlsx",
            UploadedAt = DateTime.Now,
            Status = ImportBatchStatus.EnAttenteValidation,
            TotalRows = 4
        };
        _db.ImportBatches.Add(batch);
        await _db.SaveChangesAsync();

        AddImportLines(
            CreateImportLine(batch.Id, 2, "CONF-NEW1", "Nouveau produit 1", 10, 4m, 12m, ImportLineAction.CreationProduit),
            CreateImportLine(batch.Id, 3, "CONF-NEW2", "Nouveau produit 2", 5, 3m, 8m, ImportLineAction.CreationProduit),
            CreateImportLine(batch.Id, 4, "CONF-EXIST", "Lot sur existant", 7, 6m, 14m, ImportLineAction.NouveauLot, existing.Id),
            CreateImportLine(batch.Id, 5, "CONF-EXIST", "Mise à jour prix existant", 0, 9m, 19m, ImportLineAction.MiseAJourPrix, existing.Id));

        await SaveImportLinesAsync();
        return batch.Id;
    }

    private static ImportLine CreateImportLine(
        int batchId,
        int rowNumber,
        string cip,
        string libelle,
        int qtefact,
        decimal pxFab,
        decimal pph,
        ImportLineAction action,
        int? matchedProductId = null)
    {
        return new ImportLine
        {
            ImportBatchId = batchId,
            RowNumber = rowNumber,
            RawCip = cip,
            RawLibelle = libelle,
            RawQtefact = qtefact,
            RawPxFab = pxFab,
            RawPph = pph,
            ResolvedAction = action,
            MatchedProductId = matchedProductId
        };
    }

    private void AddImportLines(params ImportLine[] lines)
    {
        foreach (var line in lines)
        {
            _db.ImportLines.Add(line);
            _pendingImportLines.Add((line, line.ResolvedAction));
        }
    }

    private async Task SaveImportLinesAsync()
    {
        await _db.SaveChangesAsync();

        foreach (var (line, action) in _pendingImportLines)
        {
            await _db.ImportLines
                .Where(l => l.Id == line.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.ResolvedAction, action));
            line.ResolvedAction = action;
        }

        _pendingImportLines.Clear();
    }
}
