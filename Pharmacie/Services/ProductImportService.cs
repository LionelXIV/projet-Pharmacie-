using Microsoft.EntityFrameworkCore;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Models.Dto;

namespace Pharmacie.Services;

/// <summary>
/// Orchestration des imports produits. Cette étape ne couvre que la prévisualisation (persistance ImportBatch/Line/Anomaly).
/// </summary>
public class ProductImportService
{
    private const int SaveBatchSize = 500;
    private const string DefaultImportCategoryName = "À catégoriser";
    private const string DefaultImportSupplierName = "Fournisseur non précisé";

    private readonly ApplicationDbContext _db;
    private readonly ExcelReaderService _excelReader;
    private readonly ImportValidationService _validation;
    private readonly ImportMatchingService _matching;
    private readonly InventoryService _inventory;

    public ProductImportService(
        ApplicationDbContext db,
        ExcelReaderService excelReader,
        ImportValidationService validation,
        ImportMatchingService matching,
        InventoryService inventory)
    {
        _db = db;
        _excelReader = excelReader;
        _validation = validation;
        _matching = matching;
        _inventory = inventory;
    }

    public async Task<int> PrepareImportAsync(Stream fileStream, string fileName, string uploadedByUserId)
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Le nom de fichier est obligatoire.", nameof(fileName));

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var batch = new ImportBatch
            {
                FileName = fileName.Trim(),
                UploadedAt = DateTime.Now,
                UploadedByUserId = uploadedByUserId,
                Status = ImportBatchStatus.EnAttenteValidation,
                TotalRows = 0
            };

            _db.ImportBatches.Add(batch);
            await _db.SaveChangesAsync();

            var rows = await _excelReader.ReadProductRowsAsync(fileStream);
            var validatedRows = _validation.ValidateRows(rows);
            var matchedRows = await _matching.MatchRowsAsync(validatedRows);

            for (var i = 0; i < matchedRows.Count; i++)
            {
                _db.ImportLines.Add(MapToImportLine(batch.Id, matchedRows[i]));

                if ((i + 1) % SaveBatchSize == 0)
                    await _db.SaveChangesAsync();
            }

            batch.TotalRows = matchedRows.Count;
            await _db.SaveChangesAsync();

            await tx.CommitAsync();
            return batch.Id;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<ImportBatchPreviewSummary> GetPreviewSummaryAsync(int importBatchId)
    {
        var batch = await _db.ImportBatches
            .AsNoTracking()
            .Where(b => b.Id == importBatchId)
            .Select(b => new { b.Id, b.TotalRows })
            .FirstOrDefaultAsync();

        if (batch == null)
            throw new InvalidOperationException($"Lot d'import #{importBatchId} introuvable.");

        var actionCounts = await _db.ImportLines
            .AsNoTracking()
            .Where(l => l.ImportBatchId == importBatchId)
            .GroupBy(l => l.ResolvedAction)
            .Select(g => new { Action = g.Key, Count = g.Count() })
            .ToListAsync();

        var anomalyCounts = await _db.ImportAnomalies
            .AsNoTracking()
            .Where(a => a.ImportLine!.ImportBatchId == importBatchId)
            .GroupBy(a => a.Severity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToListAsync();

        var totalAnomalies = anomalyCounts.Sum(x => x.Count);

        int CountAction(ImportLineAction action) =>
            actionCounts.FirstOrDefault(x => x.Action == action)?.Count ?? 0;

        return new ImportBatchPreviewSummary
        {
            ImportBatchId = batch.Id,
            TotalRows = batch.TotalRows,
            CreationProduitCount = CountAction(ImportLineAction.CreationProduit),
            MiseAJourPrixCount = CountAction(ImportLineAction.MiseAJourPrix),
            NouveauLotCount = CountAction(ImportLineAction.NouveauLot),
            IgnoreeCount = CountAction(ImportLineAction.Ignoree),
            AnomalieCount = CountAction(ImportLineAction.Anomalie),
            TotalAnomaliesCount = totalAnomalies,
            BlockingAnomaliesCount = anomalyCounts
                .FirstOrDefault(x => x.Severity == ImportAnomalySeverity.Bloquante)?.Count ?? 0,
            WarningAnomaliesCount = anomalyCounts
                .FirstOrDefault(x => x.Severity == ImportAnomalySeverity.Avertissement)?.Count ?? 0
        };
    }

    private static ImportLine MapToImportLine(int importBatchId, MatchedImportRow matched)
    {
        var source = matched.ValidatedRow.SourceRow;
        var line = new ImportLine
        {
            ImportBatchId = importBatchId,
            RowNumber = source.RowNumber,
            RawCip = source.Cip,
            RawRefha = source.Refha,
            RawLibelle = source.Libelle,
            RawQtefact = source.Qtefact,
            RawPxFab = source.PxFab,
            RawPph = source.Pph,
            ResolvedAction = matched.ResolvedAction,
            MatchedProductId = matched.MatchedProductId,
            CreatedBatchId = null
        };

        foreach (var detected in matched.ValidatedRow.Anomalies)
        {
            line.Anomalies.Add(new ImportAnomaly
            {
                AnomalyType = detected.AnomalyType,
                Severity = detected.Severity,
                Details = detected.Details,
                ResolvedByUser = false,
                Resolution = null
            });
        }

        return line;
    }

    /// <summary>
    /// Recalcule l'action résolue d'une ligne comme si elle n'avait plus d'anomalie bloquante,
    /// en tenant compte de l'ordre des lignes éligibles du lot.
    /// </summary>
    public async Task<(ImportLineAction Action, int? MatchedProductId)> ResolveActionAfterAnomalyResolutionAsync(
        ImportLine targetLine,
        IReadOnlyList<ImportLine> batchLines)
    {
        ArgumentNullException.ThrowIfNull(targetLine);
        ArgumentNullException.ThrowIfNull(batchLines);

        var ordered = batchLines.OrderBy(l => l.RowNumber).ToList();
        var cipsToLookup = ordered
            .Where(l => !HasUnresolvedBlockingAnomaly(l) && !string.IsNullOrWhiteSpace(l.RawCip))
            .Select(l => l.RawCip!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        var productIdByCip = await LoadProductIdsByCipAsync(cipsToLookup);
        var firstOccurrenceRowByCip = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var line in ordered)
        {
            if (HasUnresolvedBlockingAnomaly(line))
                continue;

            var cip = line.RawCip?.Trim();
            if (string.IsNullOrEmpty(cip))
            {
                if (line.Id == targetLine.Id)
                    return (ImportLineAction.Ignoree, null);
                continue;
            }

            if (line.Id == targetLine.Id)
            {
                if (productIdByCip.TryGetValue(cip, out var productId))
                {
                    return (line.RawQtefact > 0
                        ? ImportLineAction.NouveauLot
                        : ImportLineAction.MiseAJourPrix, productId);
                }

                if (firstOccurrenceRowByCip.ContainsKey(cip))
                    return (ImportLineAction.NouveauLot, null);

                return (ImportLineAction.CreationProduit, null);
            }

            if (productIdByCip.TryGetValue(cip, out _))
                firstOccurrenceRowByCip.TryAdd(cip, line.RowNumber);
            else if (!firstOccurrenceRowByCip.ContainsKey(cip))
                firstOccurrenceRowByCip[cip] = line.RowNumber;
        }

        return (ImportLineAction.Ignoree, null);
    }

    private static bool HasUnresolvedBlockingAnomaly(ImportLine line) =>
        line.Anomalies.Any(a => a.Severity == ImportAnomalySeverity.Bloquante && !a.ResolvedByUser);

    private async Task<Dictionary<string, int>> LoadProductIdsByCipAsync(HashSet<string> cipsToLookup)
    {
        if (cipsToLookup.Count == 0)
            return new Dictionary<string, int>(StringComparer.Ordinal);

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.Cip != null && p.Cip != "")
            .Select(p => new { p.Id, p.Cip })
            .ToListAsync();

        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var product in products)
        {
            var key = product.Cip!.Trim();
            if (key.Length == 0 || !cipsToLookup.Contains(key))
                continue;

            map.TryAdd(key, product.Id);
        }

        return map;
    }

    public async Task ConfirmImportAsync(int importBatchId, string confirmedByUserId)
    {
        if (string.IsNullOrWhiteSpace(confirmedByUserId))
            throw new ArgumentException("L'identifiant utilisateur est obligatoire.", nameof(confirmedByUserId));

        var batch = await _db.ImportBatches
            .FirstOrDefaultAsync(b => b.Id == importBatchId);

        if (batch == null)
            throw new InvalidOperationException($"Lot d'import #{importBatchId} introuvable.");

        if (batch.Status == ImportBatchStatus.Confirme)
            throw new InvalidOperationException($"Le lot d'import #{importBatchId} a déjà été confirmé.");

        if (batch.Status == ImportBatchStatus.Annule)
            throw new InvalidOperationException($"Le lot d'import #{importBatchId} a été annulé et ne peut pas être confirmé.");

        if (batch.Status != ImportBatchStatus.EnAttenteValidation)
            throw new InvalidOperationException($"Le lot d'import #{importBatchId} n'est pas en attente de validation.");

        var hasUnresolvedBlocking = await _db.ImportAnomalies
            .AnyAsync(a => a.ImportLine!.ImportBatchId == importBatchId
                && a.Severity == ImportAnomalySeverity.Bloquante
                && !a.ResolvedByUser);

        if (hasUnresolvedBlocking)
            throw new ProductImportUnresolvedAnomaliesException(
                "Des anomalies bloquantes non résolues subsistent. Traitez-les avant de confirmer l'import.");

        var lines = await _db.ImportLines
            .Where(l => l.ImportBatchId == importBatchId)
            .OrderBy(l => l.RowNumber)
            .ToListAsync();

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            var categoryId = await GetOrCreateCategoryIdAsync(DefaultImportCategoryName);
            var supplierId = await GetOrCreateSupplierIdAsync(DefaultImportSupplierName);

            var productIdByCreationLineId = new Dictionary<int, int>();

            foreach (var line in lines.Where(l => l.ResolvedAction == ImportLineAction.CreationProduit))
            {
                var product = new Product
                {
                    Cip = line.RawCip?.Trim(),
                    Refha = line.RawRefha?.Trim(),
                    CommercialName = string.IsNullOrWhiteSpace(line.RawLibelle)
                        ? line.RawCip?.Trim() ?? $"Produit import ligne {line.RowNumber}"
                        : line.RawLibelle.Trim(),
                    ReferencePurchasePrice = line.RawPxFab,
                    RegulatedSalePrice = line.RawPph,
                    SalePrice = line.RawPph ?? 0,
                    PurchasePrice = line.RawPxFab ?? 0,
                    CategoryId = categoryId,
                    SupplierId = supplierId,
                    ProductType = ProductType.Inconnu,
                    StockQuantity = 0
                };

                _db.Products.Add(product);
                await _db.SaveChangesAsync();

                productIdByCreationLineId[line.Id] = product.Id;
                line.MatchedProductId = product.Id;
            }

            // Date d'expiration provisoire : le fichier importé ne fournit pas les vraies dates.
            // Ces lots doivent être corrigés manuellement par la pharmacie après l'import initial.
            var provisionalExpirationDate = DateTime.Today.AddYears(2);

            foreach (var line in lines.Where(l =>
                (l.ResolvedAction == ImportLineAction.CreationProduit
                    || l.ResolvedAction == ImportLineAction.NouveauLot)
                && l.RawQtefact > 0))
            {
                var productId = line.ResolvedAction == ImportLineAction.CreationProduit
                    ? productIdByCreationLineId[line.Id]
                    : line.MatchedProductId
                        ?? throw new InvalidOperationException(
                            $"La ligne Excel {line.RowNumber} (nouveau lot) n'a pas de produit associé.");

                var lotNumber = $"IMPORT-{importBatchId}-{line.RowNumber}";
                var reason =
                    $"Import initial catalogue — ImportBatch #{importBatchId}, ligne Excel {line.RowNumber}, date d'expiration provisoire à corriger";

                var (ok, err) = await _inventory.StageEntreeAsync(
                    productId,
                    lotNumber,
                    provisionalExpirationDate,
                    line.RawQtefact!.Value,
                    reason,
                    confirmedByUserId);

                if (!ok)
                    throw new InvalidOperationException(err ?? $"Entrée stock impossible pour la ligne Excel {line.RowNumber}.");

                var createdBatch = _db.ProductBatches.Local
                    .FirstOrDefault(b => b.ProductId == productId && b.LotNumber == lotNumber);

                if (createdBatch != null)
                    createdBatch.SourceImportLineId = line.Id;
            }

            var matchedProductIds = lines
                .Where(l => l.MatchedProductId.HasValue)
                .Select(l => l.MatchedProductId!.Value)
                .Distinct()
                .ToList();

            if (matchedProductIds.Count > 0)
            {
                var productsById = await _db.Products
                    .Where(p => matchedProductIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                foreach (var line in lines.Where(l => l.MatchedProductId.HasValue))
                {
                    if (!productsById.TryGetValue(line.MatchedProductId!.Value, out var product))
                        continue;

                    product.ReferencePurchasePrice = line.RawPxFab;
                    product.RegulatedSalePrice = line.RawPph;
                }
            }

            batch.Status = ImportBatchStatus.Confirme;
            batch.ConfirmedAt = DateTime.Now;
            batch.ConfirmedByUserId = confirmedByUserId;

            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<ProductImportResultViewModel> GetImportResultAsync(int importBatchId)
    {
        var batch = await _db.ImportBatches
            .AsNoTracking()
            .Where(b => b.Id == importBatchId)
            .Select(b => new
            {
                b.Id,
                b.FileName,
                b.UploadedAt,
                b.ConfirmedAt,
                b.Status
            })
            .FirstOrDefaultAsync();

        if (batch == null)
            throw new InvalidOperationException($"Lot d'import #{importBatchId} introuvable.");

        if (batch.Status != ImportBatchStatus.Confirme)
            throw new InvalidOperationException($"Le lot d'import #{importBatchId} n'a pas encore été confirmé.");

        var actionCounts = await _db.ImportLines
            .AsNoTracking()
            .Where(l => l.ImportBatchId == importBatchId)
            .GroupBy(l => l.ResolvedAction)
            .Select(g => new { Action = g.Key, Count = g.Count() })
            .ToListAsync();

        int CountAction(ImportLineAction action) =>
            actionCounts.FirstOrDefault(x => x.Action == action)?.Count ?? 0;

        var batchesCreatedCount = await _db.ImportLines
            .AsNoTracking()
            .CountAsync(l => l.ImportBatchId == importBatchId
                && l.RawQtefact > 0
                && (l.ResolvedAction == ImportLineAction.CreationProduit
                    || l.ResolvedAction == ImportLineAction.NouveauLot));

        var productsUpdatedCount = await _db.ImportLines
            .AsNoTracking()
            .CountAsync(l => l.ImportBatchId == importBatchId
                && l.MatchedProductId != null
                && l.ResolvedAction != ImportLineAction.CreationProduit);

        var anomalyCounts = await _db.ImportAnomalies
            .AsNoTracking()
            .Where(a => a.ImportLine!.ImportBatchId == importBatchId)
            .GroupBy(a => a.ResolvedByUser)
            .Select(g => new { Resolved = g.Key, Count = g.Count() })
            .ToListAsync();

        return new ProductImportResultViewModel
        {
            ImportBatchId = batch.Id,
            FileName = batch.FileName,
            UploadedAt = batch.UploadedAt,
            ConfirmedAt = batch.ConfirmedAt,
            ProductsCreatedCount = CountAction(ImportLineAction.CreationProduit),
            BatchesCreatedCount = batchesCreatedCount,
            ProductsUpdatedCount = productsUpdatedCount,
            IgnoredLinesCount = CountAction(ImportLineAction.Ignoree),
            TotalAnomaliesCount = anomalyCounts.Sum(x => x.Count),
            ResolvedAnomaliesCount = anomalyCounts
                .FirstOrDefault(x => x.Resolved)?.Count ?? 0
        };
    }

    private async Task<int> GetOrCreateCategoryIdAsync(string name)
    {
        var existing = await _db.Categories
            .FirstOrDefaultAsync(c => c.Name == name);

        if (existing != null)
            return existing.Id;

        var category = new Category { Name = name };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        return category.Id;
    }

    private async Task<int> GetOrCreateSupplierIdAsync(string name)
    {
        var existing = await _db.Suppliers
            .FirstOrDefaultAsync(c => c.Name == name);

        if (existing != null)
            return existing.Id;

        var supplier = new Supplier { Name = name };
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        return supplier.Id;
    }
}

public sealed class ProductImportUnresolvedAnomaliesException : InvalidOperationException
{
    public ProductImportUnresolvedAnomaliesException(string message) : base(message)
    {
    }
}
