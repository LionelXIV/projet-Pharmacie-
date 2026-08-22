using Pharmacie.Services;
using Xunit;

namespace Pharmacie.Tests;

public class BlImportParserUbiPharmTests
{
    [Fact]
    public void ParserUbiPharm_extrait_8_produits_dont_vikvit_c_sur_2_lots()
    {
        var ocr = """
            BEL/812307
            UBIPHARM SENEGAL
            BORDEREAU

            3400931
            DOLIPRANE 1000MG
            LOT A12 PER. 15/01/27
            450,00

            3400932
            EFFERALGAN 500
            LOT B3 PER. 03/03/26
            320,50

            3400933
            VIKVIT-C 500MG
            LOT VC01 PER. 04/04/26
            LOT VC02 PER. 08/08/26
            890,00

            3400934
            AMOXICILLINE 1G
            LOT CX9 PER. 11/11/27
            1250,00

            3400935
            SMECTA 3G
            LOT SM1 PER. 02/02/28
            75,25

            3400936
            SPASFON LYOC
            LOT SP2 PER. 06/06/27
            210,00

            3400937
            GAVISCON SUSP
            LOT GV8 PER. 09/09/26
            980,00

            3400938
            DAFLON 500MG
            LOT DF4 PER. 12/12/27
            1560,00
            """;

        var lignes = BlImportService.ParserUbiPharm(ocr);

        Assert.Equal(9, lignes.Count);
        Assert.Equal(8, lignes.Select(l => l.CIP).Distinct().Count());

        var vikvit = lignes.Where(l => l.CIP == "3400933").ToList();
        Assert.Equal(2, vikvit.Count);
        Assert.All(vikvit, l => Assert.Contains("VIKVIT-C", l.NomProduit, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(vikvit, l => l.NumeroLot == "VC01");
        Assert.Contains(vikvit, l => l.NumeroLot == "VC02");
        Assert.Contains(vikvit, l => l.DatePeremption == new DateTime(2026, 4, 4));
        Assert.Contains(vikvit, l => l.DatePeremption == new DateTime(2026, 8, 8));
    }
}
