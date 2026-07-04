using ClosedXML.Excel;
using Pharmacie.Services;
using Pharmacie.Tests.Support;
using Xunit;

namespace Pharmacie.Tests;

/// <summary>
/// Tests de lecture Excel : colonnes attendues, préservation du CIP texte, colonnes obligatoires.
/// </summary>
public class ExcelReaderServiceTests
{
    private readonly ExcelReaderService _reader = new();

    [Fact]
    public async Task ReadProductRows_reads_expected_columns_from_minimal_workbook()
    {
        await using var stream = ImportTestExcelBuilder.CreateWorkbook(ws =>
        {
            ImportTestExcelBuilder.WriteRow(ws, 2, "3400930001234", "Doliprane 500 mg", 12, 3.50m, 5.99m, refha: "REF-001");
            ImportTestExcelBuilder.WriteRow(ws, 3, "3400930005678", "Spasfon", 0, 4.20m, 7.10m);
        });

        var rows = await _reader.ReadProductRowsAsync(stream);

        Assert.Equal(2, rows.Count);

        var first = rows[0];
        Assert.Equal(2, first.RowNumber);
        Assert.Equal("3400930001234", first.Cip);
        Assert.Equal("REF-001", first.Refha);
        Assert.Equal("Doliprane 500 mg", first.Libelle);
        Assert.Equal(12, first.Qtefact);
        Assert.Equal(3.50m, first.PxFab);
        Assert.Equal(5.99m, first.Pph);

        var second = rows[1];
        Assert.Equal("3400930005678", second.Cip);
        Assert.Equal(0, second.Qtefact);
    }

    [Fact]
    public async Task ReadProductRows_preserves_cip_with_significant_leading_zeros_as_string()
    {
        await using var stream = ImportTestExcelBuilder.CreateWorkbook(ws =>
        {
            ImportTestExcelBuilder.WriteRow(ws, 2, "0012345", "Produit zéros", 1, 1m, 2m, cipAsText: true);
        });

        var rows = await _reader.ReadProductRowsAsync(stream);

        Assert.Single(rows);
        Assert.Equal("0012345", rows[0].Cip);
    }

    [Fact]
    public async Task ReadProductRows_throws_when_required_column_is_missing()
    {
        await using var stream = ImportTestExcelBuilder.CreateWorkbook(
            ws => ImportTestExcelBuilder.WriteRow(ws, 2, "CIP001", "Test", 1, 1m, 2m),
            headers: ["CIP", "REFHA", "LIBELLE", "QTEFACT", "PX_FAB"]);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _reader.ReadProductRowsAsync(stream));

        Assert.Contains("Colonne obligatoire absente", ex.Message, StringComparison.Ordinal);
        Assert.Contains("PPH", ex.Message, StringComparison.Ordinal);
    }
}
