using System.Text.RegularExpressions;
using Pharmacie.Models;
using Pharmacie.Models.Dto;

namespace Pharmacie.Services;

/// <summary>
/// Validation métier des lignes lues depuis un fichier d'import. Aucune persistance ni accès base de données.
/// </summary>
public partial class ImportValidationService
{
    public List<ValidatedImportRow> ValidateRows(List<ExcelImportRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var results = rows
            .Select(row => new ValidatedImportRow
            {
                SourceRow = row,
                Anomalies = new List<DetectedAnomaly>()
            })
            .ToList();

        for (var i = 0; i < rows.Count; i++)
            ApplyPerRowRules(rows[i], results[i].Anomalies);

        ApplyDuplicateCipRules(rows, results);
        ApplyCipLibelleConsistencyRules(rows, results);

        return results;
    }

    private static void ApplyPerRowRules(ExcelImportRow row, List<DetectedAnomaly> anomalies)
    {
        if (string.IsNullOrWhiteSpace(row.Cip))
        {
            anomalies.Add(new DetectedAnomaly
            {
                AnomalyType = ImportAnomalyType.CipManquantOuInvalide,
                Severity = ImportAnomalySeverity.Bloquante,
                Details = $"Ligne {row.RowNumber} : CIP manquant ou vide."
            });
        }

        if (string.IsNullOrWhiteSpace(row.Libelle))
        {
            anomalies.Add(new DetectedAnomaly
            {
                AnomalyType = ImportAnomalyType.LibelleVide,
                Severity = ImportAnomalySeverity.Bloquante,
                Details = $"Ligne {row.RowNumber} : libellé manquant ou vide."
            });
        }

        if (row.Pph is null or 0)
        {
            anomalies.Add(new DetectedAnomaly
            {
                AnomalyType = ImportAnomalyType.PphZeroOuInferieurAuPrixFab,
                Severity = ImportAnomalySeverity.Bloquante,
                Details = $"Ligne {row.RowNumber} : PPH absent ou égal à zéro."
            });
        }
        else if (row.PxFab.HasValue && row.Pph > 0 && row.Pph <= row.PxFab)
        {
            anomalies.Add(new DetectedAnomaly
            {
                AnomalyType = ImportAnomalyType.PphZeroOuInferieurAuPrixFab,
                Severity = ImportAnomalySeverity.Avertissement,
                Details = $"Ligne {row.RowNumber} : PPH ({row.Pph:0.00}) inférieur ou égal au prix fab. ({row.PxFab:0.00})."
            });
        }

        if (row.Qtefact < 0)
        {
            anomalies.Add(new DetectedAnomaly
            {
                AnomalyType = ImportAnomalyType.QuantiteNegative,
                Severity = ImportAnomalySeverity.Avertissement,
                Details = $"Ligne {row.RowNumber} : quantité facturée négative ({row.Qtefact})."
            });
        }
    }

    private static void ApplyDuplicateCipRules(IReadOnlyList<ExcelImportRow> rows, IList<ValidatedImportRow> results)
    {
        var groups = rows
            .Select((row, index) => (row, index))
            .Where(x => !string.IsNullOrWhiteSpace(x.row.Cip))
            .GroupBy(x => x.row.Cip!.Trim(), StringComparer.Ordinal);

        foreach (var group in groups.Where(g => g.Count() > 1))
        {
            var cip = group.Key;
            foreach (var (_, index) in group)
            {
                results[index].Anomalies.Add(new DetectedAnomaly
                {
                    AnomalyType = ImportAnomalyType.CipDupliqueDansLeFichier,
                    Severity = ImportAnomalySeverity.Avertissement,
                    Details = $"Ligne {rows[index].RowNumber} : CIP « {cip} » présent sur {group.Count()} lignes du fichier."
                });
            }
        }
    }

    private static void ApplyCipLibelleConsistencyRules(IReadOnlyList<ExcelImportRow> rows, IList<ValidatedImportRow> results)
    {
        var groups = rows
            .Select((row, index) => (row, index))
            .Where(x => !string.IsNullOrWhiteSpace(x.row.Cip))
            .GroupBy(x => x.row.Cip!.Trim(), StringComparer.Ordinal);

        foreach (var group in groups.Where(g => g.Count() > 1))
        {
            var distinctLibelles = group
                .Select(x => NormalizeLibelleForComparison(x.row.Libelle))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (distinctLibelles.Count <= 1)
                continue;

            var cip = group.Key;
            var libelleSamples = group
                .Select(x => x.row.Libelle?.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3);

            var detailSuffix = string.Join(" / ", libelleSamples);

            foreach (var (_, index) in group)
            {
                results[index].Anomalies.Add(new DetectedAnomaly
                {
                    AnomalyType = ImportAnomalyType.CipIdentiqueLibelleDifferent,
                    Severity = ImportAnomalySeverity.Bloquante,
                    Details = $"Ligne {rows[index].RowNumber} : CIP « {cip} » associé à des libellés différents ({detailSuffix})."
                });
            }
        }
    }

    /// <summary>Trim, casse ignorée, espaces internes normalisés.</summary>
    private static string NormalizeLibelleForComparison(string? libelle)
    {
        if (string.IsNullOrWhiteSpace(libelle))
            return string.Empty;

        var collapsed = WhitespaceCollapse().Replace(libelle.Trim(), " ");
        return collapsed.ToUpperInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceCollapse();
}
