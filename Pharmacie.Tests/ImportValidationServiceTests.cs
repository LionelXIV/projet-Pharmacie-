using Pharmacie.Models;
using Pharmacie.Models.Dto;
using Pharmacie.Services;
using Xunit;

namespace Pharmacie.Tests;

/// <summary>
/// Tests des règles de validation métier sur les lignes d'import.
/// </summary>
public class ImportValidationServiceTests
{
    private readonly ImportValidationService _validation = new();

    [Fact]
    public void ValidateRows_pph_zero_produces_blocking_pph_anomaly()
    {
        var rows = new List<ExcelImportRow>
        {
            new() { RowNumber = 2, Cip = "CIP001", Libelle = "Produit", Qtefact = 1, PxFab = 5m, Pph = 0 }
        };

        var results = _validation.ValidateRows(rows);

        Assert.Single(results);
        var anomaly = Assert.Single(results[0].Anomalies);
        Assert.Equal(ImportAnomalyType.PphZeroOuInferieurAuPrixFab, anomaly.AnomalyType);
        Assert.Equal(ImportAnomalySeverity.Bloquante, anomaly.Severity);
    }

    [Fact]
    public void ValidateRows_negative_quantity_produces_warning()
    {
        var rows = new List<ExcelImportRow>
        {
            new() { RowNumber = 2, Cip = "CIP001", Libelle = "Produit", Qtefact = -3, PxFab = 5m, Pph = 10m }
        };

        var results = _validation.ValidateRows(rows);

        var anomaly = Assert.Single(results[0].Anomalies, a => a.AnomalyType == ImportAnomalyType.QuantiteNegative);
        Assert.Equal(ImportAnomalySeverity.Avertissement, anomaly.Severity);
    }

    [Fact]
    public void ValidateRows_duplicate_cip_same_libelle_produces_warning_on_both_lines_without_blocking_libelle_anomaly()
    {
        var rows = new List<ExcelImportRow>
        {
            new() { RowNumber = 2, Cip = "CIP-DUP", Libelle = "Même libellé", Qtefact = 1, PxFab = 5m, Pph = 10m },
            new() { RowNumber = 3, Cip = "CIP-DUP", Libelle = "Même libellé", Qtefact = 2, PxFab = 5m, Pph = 10m }
        };

        var results = _validation.ValidateRows(rows);

        Assert.Equal(2, results.Count);
        foreach (var result in results)
        {
            Assert.Contains(result.Anomalies, a =>
                a.AnomalyType == ImportAnomalyType.CipDupliqueDansLeFichier
                && a.Severity == ImportAnomalySeverity.Avertissement);
            Assert.DoesNotContain(result.Anomalies, a =>
                a.AnomalyType == ImportAnomalyType.CipIdentiqueLibelleDifferent);
            Assert.DoesNotContain(result.Anomalies, a => a.Severity == ImportAnomalySeverity.Bloquante);
        }
    }

    [Fact]
    public void ValidateRows_duplicate_cip_different_libelle_produces_blocking_consistency_anomaly()
    {
        var rows = new List<ExcelImportRow>
        {
            new() { RowNumber = 2, Cip = "CIP-DIFF", Libelle = "Libellé A", Qtefact = 1, PxFab = 5m, Pph = 10m },
            new() { RowNumber = 3, Cip = "CIP-DIFF", Libelle = "Libellé B", Qtefact = 1, PxFab = 5m, Pph = 10m }
        };

        var results = _validation.ValidateRows(rows);

        Assert.Equal(2, results.Count);
        foreach (var result in results)
        {
            var blocking = Assert.Single(result.Anomalies, a =>
                a.AnomalyType == ImportAnomalyType.CipIdentiqueLibelleDifferent);
            Assert.Equal(ImportAnomalySeverity.Bloquante, blocking.Severity);
        }
    }
}
