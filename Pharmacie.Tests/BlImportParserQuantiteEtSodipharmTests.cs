using Pharmacie.Services;
using Xunit;

namespace Pharmacie.Tests;

public class BlImportParserUbiPharmQuantiteTests
{
    [Fact]
    public void ParserUbiPharm_quantite_via_montant_sur_prix_pas_numero_de_ligne()
    {
        var ocr = """
            BEL/812307
            UBIPHARM SENEGAL
            BORDEREAU

            16
            3400931
            DOLIPRANE 1000MG
            LOT A12 PER. 15/01/27
            450,00
            900,00
            17
            3400932
            EFFERALGAN 500
            LOT B3 PER. 03/03/26
            320,50
            641,00
            18
            3400933
            VIKVIT-C 500MG
            LOT VC01 PER. 04/04/26
            LOT VC02 PER. 08/08/26
            890,00
            1780,00
            19
            3400934
            AMOXICILLINE 1G
            LOT CX9 PER. 11/11/27
            1250,00
            2500,00
            20
            3400935
            SMECTA 3G
            LOT SM1 PER. 02/02/28
            75,25
            150,50
            21
            3400936
            LOT SP2 PER. 06/06/27
            SPASFON LYOC
            210,00
            420,00
            22
            3400937
            GAVISCON SUSP
            LOT GV8 PER. 09/09/26
            980,00
            1960,00
            23
            3400938
            TRETINEX CREAM
            LOT TX1 PER. 12/12/27
            1560,00
            3120,00
            """;

        var lignes = BlImportService.ParserUbiPharm(ocr);

        Assert.Equal(9, lignes.Count);
        Assert.Equal(8, lignes.Select(l => l.CIP).Distinct().Count());

        var spasfon = lignes.Single(l => l.CIP == "3400936");
        Assert.Contains("SPASFON", spasfon.NomProduit, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LOT", spasfon.NomProduit, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, spasfon.QuantiteLivree);
        Assert.NotEqual(22, spasfon.QuantiteLivree);
        Assert.Equal("bonne", spasfon.Confiance);

        var vikvit = lignes.Where(l => l.CIP == "3400933").ToList();
        Assert.Equal(2, vikvit.Count);
        Assert.All(vikvit, l => Assert.Equal(2, l.QuantiteLivree));
        Assert.All(vikvit, l => Assert.Equal("bonne", l.Confiance));

        Assert.Equal(2, lignes.Single(l => l.CIP == "3400931").QuantiteLivree);
        Assert.Equal(2, lignes.Single(l => l.CIP == "3400938").QuantiteLivree);
        Assert.Contains("TRETINEX", lignes.Single(l => l.CIP == "3400938").NomProduit, StringComparison.OrdinalIgnoreCase);
    }
}

public class BlImportParserSodipharmTests
{
    [Fact]
    public void ParserSodipharm_extrait_qt_livree_prix_et_rupture()
    {
        var ocr = """
            SODIPHARM
            BORDEREAU DE LIVRAISON
            3400123  5  3  DOLIPRANE 500
            1250,00  18  1800,00
            3400124  2  0  AMOXICILLINE 1G
            PAS DE SUIVI DES MANQUANTS
            800,00  18  1200,00
            03-27
            """;

        var lignes = BlImportService.ParserSodipharm(ocr);
        Assert.Equal(2, lignes.Count);

        var doli = lignes.Single(l => l.CIP == "3400123");
        Assert.Equal(3, doli.QuantiteLivree);
        Assert.Equal(1250.00m, doli.PrixAchat);
        Assert.Equal(1800.00m, doli.PrixVente);
        Assert.Equal(18m, doli.TauxTVA);
        Assert.Contains("DOLIPRANE", doli.NomProduit, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("bonne", doli.Confiance);

        var rupture = lignes.Single(l => l.CIP == "3400124");
        Assert.Equal(0, rupture.QuantiteLivree);
        Assert.Equal("rupture", rupture.Confiance);
        Assert.NotNull(rupture.DatePeremption);
        Assert.Equal(3, rupture.DatePeremption!.Value.Month);
        Assert.Equal(2027, rupture.DatePeremption.Value.Year);
    }
}
