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

    [Fact]
    public void ParserSodipharm_pdf_une_ligne_par_produit_comme_pdfpig()
    {
        var texte = """
            SODIPHARM
            BORDEREAU DE LIVRAISON
            26. N . 0104        1           1 BACTOX SS SUCR SUSP 125MG 60ML 3335882        1249 T   859       859
            36. J . 0501        2           2 AMLOPAMIDE 10MG/1MG5 CPR BT 30 2475717        5647 T  3986      3986
            LIGNES
                 2
            """;

        var lignes = BlImportService.ParserSodipharm(texte);
        Assert.Equal(2, lignes.Count);
        Assert.Equal("BACTOX SS SUCR SUSP 125MG 60ML", lignes[0].NomProduit);
        Assert.Equal(1, lignes[0].QuantiteLivree);
        Assert.Equal(859m, lignes[0].PrixAchat);
        Assert.Equal("AMLOPAMIDE 10MG/1MG5 CPR BT 30", lignes[1].NomProduit);
        Assert.Equal(2, lignes[1].QuantiteLivree);
    }

    [Fact]
    public void ParserSodipharm_texte_colle_sans_retours_ligne()
    {
        var texte = "SODIPHARM BORDEREAU DE LIVRAISON N 012978910 26. N . 0104        1           1 BACTOX SS SUCR SUSP 125MG 60ML 3335882        1249 T   859       859 36. J . 0501        2           2 AMLOPAMIDE 10MG/1MG5 CPR BT 30 2475717        5647 T  3986      3986 LIGNES 2 TOTAL HT";
        var lignes = BlImportService.ParserSodipharm(texte);
        Assert.Equal(2, lignes.Count);
        Assert.Contains(lignes, l => l.CIP == "3335882" && l.QuantiteLivree == 1);
        Assert.Contains(lignes, l => l.CIP == "2475717" && l.QuantiteLivree == 2);
    }

    [Fact]
    public void ExtraireTextePdf_puis_parsers_sur_pdf_reels()
    {
        var sodioPdf = Fixture("blsodiof.pdf");
        var ubiPdf = Fixture("ubi_bel_812307.pdf");
        if (!File.Exists(sodioPdf) || !File.Exists(ubiPdf))
            return;

        using (var fs = File.OpenRead(sodioPdf))
        {
            var texte = BlImportService.ExtraireTextePdf(fs);
            Assert.Contains("SODIPHARM", texte, StringComparison.OrdinalIgnoreCase);
            var lignes = BlImportService.ParserSodipharm(texte);
            Assert.Equal(2, lignes.Count);
            Assert.Contains(lignes, l => l.NomProduit.Contains("BACTOX", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(lignes, l => l.NomProduit.Contains("AMLOPAMIDE", StringComparison.OrdinalIgnoreCase));
        }

        using (var fs = File.OpenRead(Fixture("ubi_bel_812307.pdf")))
        {
            var texte = BlImportService.ExtraireTextePdf(fs);
            Assert.Contains("BEL/", texte, StringComparison.OrdinalIgnoreCase);
            var lignes = BlImportService.ParserUbiPharm(texte);
            Assert.True(lignes.Count >= 24, $"UbiPharm PDF: {lignes.Count} lignes");
            Assert.Contains(lignes, l => l.NomProduit.Contains("SPASFON", StringComparison.OrdinalIgnoreCase) && l.QuantiteLivree == 2);
            Assert.Equal(2, lignes.Count(l => l.NomProduit.Contains("VIKVIT", StringComparison.OrdinalIgnoreCase)));
        }
    }
}
