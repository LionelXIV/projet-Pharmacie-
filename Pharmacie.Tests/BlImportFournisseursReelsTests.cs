using Pharmacie.Services;
using Pharmacie.Tests.Support;
using Xunit;

namespace Pharmacie.Tests;

public class BlImportFournisseursReelsTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void ParserUbiPharm_pdf_facture_bel_812307_24_produits()
    {
        var texte = File.ReadAllText(Fixture("ubi_bel_812307.txt"));
        var lignes = BlImportService.ParserUbiPharm(texte);

        Assert.True(lignes.Select(l => l.CIP).Distinct().Count() >= 20);
        Assert.True(lignes.Count >= 24, $"attendu >= 24, obtenu {lignes.Count}");

        var citrate = lignes.Single(l => l.NomProduit.Contains("CITRATE BETAINE", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("3400949974092", citrate.CIP);
        Assert.Equal(1, citrate.QuantiteLivree);
        Assert.Equal(2499m, citrate.PrixAchat);
        Assert.Equal(3516m, citrate.PrixVente);
        Assert.Equal("bonne", citrate.Confiance);
        Assert.Equal("D7149", citrate.NumeroLot);

        var spasfon = lignes.Single(l => l.NomProduit.Contains("SPASFON", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, spasfon.QuantiteLivree);
        Assert.Equal(1515m, spasfon.PrixAchat);
        Assert.DoesNotContain("LOT", spasfon.NomProduit, StringComparison.OrdinalIgnoreCase);

        var vikvit = lignes.Where(l => l.NomProduit.Contains("VIKVIT", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.Equal(2, vikvit.Count);
        Assert.Contains(vikvit, l => l.NumeroLot == "ET1125004E");
        Assert.Contains(vikvit, l => l.NumeroLot == "V1525032");
    }

    [Fact]
    public void ParserSodipharm_pdf_bordereau_2_lignes()
    {
        var texte = File.ReadAllText(Fixture("sodipharm_blsodiof.txt"));
        var lignes = BlImportService.ParserSodipharm(texte);

        Assert.Equal(2, lignes.Count);
        var bactox = lignes.Single(l => l.NomProduit.Contains("BACTOX", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("3335882", bactox.CIP);
        Assert.Equal(1, bactox.QuantiteLivree);
        Assert.Equal(859m, bactox.PrixAchat);
        Assert.Equal(1249m, bactox.PrixVente);
        Assert.Equal("bonne", bactox.Confiance);

        var amlo = lignes.Single(l => l.NomProduit.Contains("AMLOPAMIDE", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("2475717", amlo.CIP);
        Assert.Equal(2, amlo.QuantiteLivree);
        Assert.Equal(3986m, amlo.PrixAchat);
    }

    [Fact]
    public async Task PreviewAsync_csv_ubi_24_lignes_sans_erreur()
    {
        var (db, conn) = TestDbContextFactory.Create();
        await using var _ = db;
        await using var __ = conn;

        var svc = new BlImportService(db);
        await using var fs = File.OpenRead(Fixture("FACTURE-BEL-812307.csv"));
        var result = await svc.PreviewAsync(fs, "FACTURE-BEL-812307.csv");

        Assert.True(result.Ok, result.Message);
        Assert.Equal(24, result.Lines.Count);
        var spasfon = result.Lines.Single(l => (l.Libelle ?? l.ProductText ?? "").Contains("SPASFON", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, spasfon.Quantite);
        Assert.Equal(1515m, spasfon.PrixAchat);
        Assert.Equal(2132m, spasfon.PrixVente);
        Assert.Equal(0m, spasfon.TauxTVA);
        var magne = result.Lines.Single(l => (l.Libelle ?? l.ProductText ?? "").Contains("MAGNESIUM", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(18m, magne.TauxTVA);
    }
}
