using System.Globalization;
using ClosedXML.Excel;
using Pharmacie.Models.Dto;

namespace Pharmacie.Services;

/// <summary>
/// Lecture brute d'un fichier .xlsx d'import produits. Aucune validation métier ni accès base de données.
/// </summary>
public class ExcelReaderService
{
    private static readonly string[] RequiredColumnNames =
    [
        "CIP",
        "LIBELLE",
        "QTEFACT",
        "PX_FAB",
        "PPH"
    ];

    private const string OptionalRefhaColumnName = "REFHA";

    private const int MaxHeaderSearchRows = 100;

    public Task<List<ExcelImportRow>> ReadProductRowsAsync(Stream fileStream)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        return Task.FromResult(ReadProductRows(fileStream));
    }

    private static List<ExcelImportRow> ReadProductRows(Stream fileStream)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.FirstOrDefault(ws => ws.LastRowUsed() != null)
            ?? throw new InvalidOperationException("Le fichier Excel ne contient aucune feuille avec des données.");

        var columnIndexByName = FindHeaderColumns(worksheet, out var headerRowNumber);
        var lastDataRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRowNumber;

        var rows = new List<ExcelImportRow>();
        for (var rowNumber = headerRowNumber + 1; rowNumber <= lastDataRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (IsRowEmpty(row, columnIndexByName))
                continue;

            rows.Add(new ExcelImportRow
            {
                RowNumber = rowNumber,
                Cip = ReadCipCell(row.Cell(columnIndexByName["CIP"])),
                Refha = columnIndexByName.TryGetValue(OptionalRefhaColumnName, out var refhaCol)
                    ? ReadTextCell(row.Cell(refhaCol))
                    : null,
                Libelle = ReadTextCell(row.Cell(columnIndexByName["LIBELLE"])),
                Qtefact = ReadNullableInt(row.Cell(columnIndexByName["QTEFACT"])),
                PxFab = ReadNullableDecimal(row.Cell(columnIndexByName["PX_FAB"])),
                Pph = ReadNullableDecimal(row.Cell(columnIndexByName["PPH"]))
            });
        }

        return rows;
    }

    private static Dictionary<string, int> FindHeaderColumns(IXLWorksheet worksheet, out int headerRowNumber)
    {
        var lastSearchRow = Math.Min(worksheet.LastRowUsed()?.RowNumber() ?? 1, MaxHeaderSearchRows);

        for (var rowNumber = 1; rowNumber <= lastSearchRow; rowNumber++)
        {
            var columnIndexByName = MapHeaderRow(worksheet.Row(rowNumber));
            if (columnIndexByName == null)
                continue;

            if (!columnIndexByName.ContainsKey("CIP"))
                continue;

            EnsureRequiredColumnsPresent(columnIndexByName);
            headerRowNumber = rowNumber;
            return columnIndexByName;
        }

        throw new InvalidOperationException("Colonne obligatoire absente : CIP");
    }

    private static void EnsureRequiredColumnsPresent(IReadOnlyDictionary<string, int> columnIndexByName)
    {
        foreach (var name in RequiredColumnNames)
        {
            if (!columnIndexByName.ContainsKey(name))
                throw new InvalidOperationException($"Colonne obligatoire absente : {name}");
        }
    }

    private static Dictionary<string, int>? MapHeaderRow(IXLRow row)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var lastColumn = row.LastCellUsed()?.Address.ColumnNumber ?? 0;
        if (lastColumn == 0)
            return null;

        for (var col = 1; col <= lastColumn; col++)
        {
            var normalized = NormalizeHeaderName(row.Cell(col).GetString());
            if (normalized.Length == 0)
                continue;

            map.TryAdd(normalized, col);
        }

        return map.Count == 0 ? null : map;
    }

    private static string NormalizeHeaderName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToUpperInvariant();

    private static bool IsRowEmpty(IXLRow row, IReadOnlyDictionary<string, int> columnIndexByName)
    {
        foreach (var columnName in RequiredColumnNames)
        {
            if (!IsCellEmpty(row.Cell(columnIndexByName[columnName])))
                return false;
        }

        if (columnIndexByName.TryGetValue(OptionalRefhaColumnName, out var refhaCol)
            && !IsCellEmpty(row.Cell(refhaCol)))
            return false;

        return true;
    }

    private static bool IsCellEmpty(IXLCell cell) =>
        cell.IsEmpty() || string.IsNullOrWhiteSpace(ReadTextCell(cell));

    /// <summary>CIP : texte brut uniquement (jamais de conversion numérique).</summary>
    private static string? ReadCipCell(IXLCell cell)
    {
        if (cell.IsEmpty())
            return null;

        var text = cell.DataType switch
        {
            XLDataType.Text => cell.GetString(),
            XLDataType.Number => cell.GetFormattedString(),
            XLDataType.Boolean => cell.GetBoolean().ToString(),
            XLDataType.DateTime => cell.GetFormattedString(),
            _ => cell.GetFormattedString()
        };

        text = text?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static string? ReadTextCell(IXLCell cell)
    {
        if (cell.IsEmpty())
            return null;

        var text = cell.GetString().Trim();
        if (text.Length > 0)
            return text;

        text = cell.GetFormattedString().Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static int? ReadNullableInt(IXLCell cell)
    {
        if (cell.IsEmpty())
            return null;

        if (cell.DataType == XLDataType.Number)
            return (int)Math.Truncate(cell.GetDouble());

        var text = cell.GetString().Trim();
        if (text.Length == 0)
            text = cell.GetFormattedString().Trim();

        if (text.Length == 0)
            return null;

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var invariant))
            return invariant;

        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out var current))
            return current;

        return null;
    }

    private static decimal? ReadNullableDecimal(IXLCell cell)
    {
        if (cell.IsEmpty())
            return null;

        if (cell.DataType == XLDataType.Number)
            return (decimal)cell.GetDouble();

        var text = cell.GetString().Trim();
        if (text.Length == 0)
            text = cell.GetFormattedString().Trim();

        if (text.Length == 0)
            return null;

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant))
            return invariant;

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out var current))
            return current;

        return null;
    }
}
