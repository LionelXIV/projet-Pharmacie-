using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pharmacie.Data;
using Pharmacie.Models;
using Pharmacie.Models.Dto;
using Pharmacie.Services;
using Pharmacie.Tests.Support;
using Xunit;

namespace Pharmacie.Tests;

/// <summary>
/// Tests du matching d'import : actions résolues selon le catalogue et les anomalies.
/// </summary>
public class ImportMatchingServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _db;
    private readonly ImportMatchingService _matching;

    public ImportMatchingServiceTests()
    {
        (_db, _connection) = TestDbContextFactory.Create();
        _matching = new ImportMatchingService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task<int> SeedProductWithCipAsync(string cip)
    {
        var (_, _, product) = TestCatalogSeed.SeedBasicCatalog(_db);
        product.Cip = cip;
        product.CommercialName = $"Produit {cip}";
        await _db.SaveChangesAsync();
        return product.Id;
    }

    private static ValidatedImportRow ValidRow(string cip, int qtefact, int rowNumber = 2) =>
        new()
        {
            SourceRow = new ExcelImportRow
            {
                RowNumber = rowNumber,
                Cip = cip,
                Libelle = $"Libellé {cip}",
                Qtefact = qtefact,
                PxFab = 5m,
                Pph = 10m
            },
            Anomalies = []
        };

    [Fact]
    public async Task MatchRows_existing_cip_with_positive_quantity_resolves_nouveau_lot()
    {
        var productId = await SeedProductWithCipAsync("EXIST-LOT");

        var results = await _matching.MatchRowsAsync([ValidRow("EXIST-LOT", qtefact: 5)]);

        Assert.Single(results);
        Assert.Equal(ImportLineAction.NouveauLot, results[0].ResolvedAction);
        Assert.Equal(productId, results[0].MatchedProductId);
    }

    [Fact]
    public async Task MatchRows_existing_cip_with_zero_quantity_resolves_mise_a_jour_prix()
    {
        var productId = await SeedProductWithCipAsync("EXIST-PRIX");

        var results = await _matching.MatchRowsAsync([ValidRow("EXIST-PRIX", qtefact: 0)]);

        Assert.Single(results);
        Assert.Equal(ImportLineAction.MiseAJourPrix, results[0].ResolvedAction);
        Assert.Equal(productId, results[0].MatchedProductId);
    }

    [Fact]
    public async Task MatchRows_new_cip_twice_in_file_first_creation_second_nouveau_lot()
    {
        var results = await _matching.MatchRowsAsync(
        [
            ValidRow("NEW-DUP", qtefact: 3, rowNumber: 2),
            ValidRow("NEW-DUP", qtefact: 1, rowNumber: 3)
        ]);

        Assert.Equal(2, results.Count);
        Assert.Equal(ImportLineAction.CreationProduit, results[0].ResolvedAction);
        Assert.Null(results[0].MatchedProductId);
        Assert.Equal(ImportLineAction.NouveauLot, results[1].ResolvedAction);
        Assert.Null(results[1].MatchedProductId);
        Assert.Equal(2, results[1].ReferenceFirstOccurrenceRowNumber);
    }

    [Fact]
    public async Task MatchRows_blocking_anomaly_resolves_ignoree_regardless_of_cip()
    {
        await SeedProductWithCipAsync("EXIST-BLOCK");

        var row = ValidRow("EXIST-BLOCK", qtefact: 5);
        row.Anomalies.Add(new DetectedAnomaly
        {
            AnomalyType = ImportAnomalyType.PphZeroOuInferieurAuPrixFab,
            Severity = ImportAnomalySeverity.Bloquante,
            Details = "PPH nul"
        });

        var results = await _matching.MatchRowsAsync([row]);

        Assert.Single(results);
        Assert.Equal(ImportLineAction.Ignoree, results[0].ResolvedAction);
        Assert.Null(results[0].MatchedProductId);
    }
}
