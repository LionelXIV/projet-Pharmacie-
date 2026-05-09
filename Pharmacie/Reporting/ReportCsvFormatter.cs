using System.Globalization;
using System.Text;

namespace Pharmacie.Reporting;

/// <summary>Échappement RFC 4180 simple et encodage UTF-8 avec BOM (Excel Windows).</summary>
public static class ReportCsvFormatter
{
    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return needsQuotes ? $"\"{escaped}\"" : escaped;
    }

    public static string DecimalInvariant(decimal d) => d.ToString("0.00", CultureInfo.InvariantCulture);

    public static string IntInvariant(int n) => n.ToString(CultureInfo.InvariantCulture);

    public static byte[] ToUtf8BytesWithBom(string csvBody) => Utf8WithBom.GetBytes(csvBody);

    public static string FileName(string slug) => $"{slug}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
}
