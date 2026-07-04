using ClosedXML.Excel;

namespace Pharmacie.Tests.Support;

/// <summary>Génère des fichiers .xlsx minimaux en mémoire pour les tests d'import.</summary>
internal static class ImportTestExcelBuilder
{
    private static readonly string[] AllHeaders = ["CIP", "REFHA", "LIBELLE", "QTEFACT", "PX_FAB", "PPH"];

    public static MemoryStream CreateWorkbook(Action<IXLWorksheet> fillRows, params string[] headers)
    {
        var headerRow = headers.Length == 0 ? AllHeaders : headers;
        var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.AddWorksheet("Import");
            for (var col = 0; col < headerRow.Length; col++)
                worksheet.Cell(1, col + 1).Value = headerRow[col];

            fillRows(worksheet);
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        return stream;
    }

    public static void WriteRow(
        IXLWorksheet worksheet,
        int row,
        string cip,
        string libelle,
        int qtefact,
        decimal pxFab,
        decimal pph,
        string? refha = null,
        bool cipAsText = false)
    {
        var cipCell = worksheet.Cell(row, 1);
        if (cipAsText)
        {
            cipCell.Style.NumberFormat.Format = "@";
            cipCell.SetValue(cip);
        }
        else
        {
            cipCell.Value = cip;
        }

        worksheet.Cell(row, 2).Value = refha ?? string.Empty;
        worksheet.Cell(row, 3).Value = libelle;
        worksheet.Cell(row, 4).Value = qtefact;
        worksheet.Cell(row, 5).Value = pxFab;
        worksheet.Cell(row, 6).Value = pph;
    }
}
