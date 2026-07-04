using Microsoft.EntityFrameworkCore;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Models.Dto;

namespace Pharmacie.Services;

/// <summary>
/// Détermine l'action résolue pour chaque ligne d'import validée. Lecture seule, aucune persistance.
/// </summary>
public class ImportMatchingService
{
    private readonly ApplicationDbContext _db;

    public ImportMatchingService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<MatchedImportRow>> MatchRowsAsync(List<ValidatedImportRow> validatedRows)
    {
        ArgumentNullException.ThrowIfNull(validatedRows);

        var productIdByCip = await LoadProductIdsByCipAsync(validatedRows);
        var firstOccurrenceRowByCip = new Dictionary<string, int>(StringComparer.Ordinal);
        var results = new List<MatchedImportRow>(validatedRows.Count);

        foreach (var validated in validatedRows)
        {
            var source = validated.SourceRow;

            if (HasBlockingAnomaly(validated))
            {
                results.Add(new MatchedImportRow
                {
                    ValidatedRow = validated,
                    ResolvedAction = ImportLineAction.Ignoree
                });
                continue;
            }

            var cip = source.Cip?.Trim();
            if (string.IsNullOrEmpty(cip))
            {
                results.Add(new MatchedImportRow
                {
                    ValidatedRow = validated,
                    ResolvedAction = ImportLineAction.Ignoree
                });
                continue;
            }

            if (productIdByCip.TryGetValue(cip, out var productId))
            {
                results.Add(new MatchedImportRow
                {
                    ValidatedRow = validated,
                    ResolvedAction = source.Qtefact > 0
                        ? ImportLineAction.NouveauLot
                        : ImportLineAction.MiseAJourPrix,
                    MatchedProductId = productId
                });

                firstOccurrenceRowByCip.TryAdd(cip, source.RowNumber);
                continue;
            }

            if (firstOccurrenceRowByCip.TryGetValue(cip, out var firstRowNumber))
            {
                results.Add(new MatchedImportRow
                {
                    ValidatedRow = validated,
                    ResolvedAction = ImportLineAction.NouveauLot,
                    ReferenceFirstOccurrenceRowNumber = firstRowNumber
                });
                continue;
            }

            firstOccurrenceRowByCip[cip] = source.RowNumber;
            results.Add(new MatchedImportRow
            {
                ValidatedRow = validated,
                ResolvedAction = ImportLineAction.CreationProduit
            });
        }

        return results;
    }

    private async Task<Dictionary<string, int>> LoadProductIdsByCipAsync(
        IReadOnlyList<ValidatedImportRow> validatedRows)
    {
        var cipsToLookup = validatedRows
            .Where(r => !HasBlockingAnomaly(r) && !string.IsNullOrWhiteSpace(r.SourceRow.Cip))
            .Select(r => r.SourceRow.Cip!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

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

    private static bool HasBlockingAnomaly(ValidatedImportRow row) =>
        row.Anomalies.Any(a => a.Severity == ImportAnomalySeverity.Bloquante);
}
