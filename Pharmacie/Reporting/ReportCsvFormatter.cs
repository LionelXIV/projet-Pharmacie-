using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Pharmacie.Reporting;

/// <summary>
/// CSV pour Excel FR : séparateur point-virgule, UTF-8 avec BOM, génération en mémoire.
/// </summary>
public static class ReportCsvFormatter
{
    public const char Separator = ';';

    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly CultureInfo FrFcfa = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>Déclare le séparateur à Excel (FR) sur la première ligne.</summary>
    public static StringBuilder CreateBuilder()
    {
        var sb = new StringBuilder();
        sb.AppendLine("sep=;");
        return sb;
    }

    public static string Join(params string[] fields) => string.Join(Separator, fields);

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        var needsQuotes = value.Contains(Separator)
            || value.Contains(',')
            || value.Contains('"')
            || value.Contains('\n')
            || value.Contains('\r');
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return needsQuotes ? $"\"{escaped}\"" : escaped;
    }

    public static string DecimalInvariant(decimal d) => d.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Affichage UI : « 1 234 FCFA » (entier, sans décimales).</summary>
    public static string FormatFcfa(decimal amount) =>
        amount.ToString("N0", FrFcfa) + " FCFA";

    /// <summary>Montant entier pour colonnes monétaires CSV (sans séparateur de milliers).</summary>
    public static string FcfaCsvAmount(decimal amount) =>
        decimal.Round(amount, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

    public static string IntInvariant(int n) => n.ToString(CultureInfo.InvariantCulture);

    public static byte[] ToUtf8BytesWithBom(string csvBody)
    {
        var preamble = Utf8WithBom.GetPreamble();
        var body = Utf8WithBom.GetBytes(csvBody ?? string.Empty);
        if (preamble.Length == 0)
            return body;

        var result = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }

    /// <summary>Nom de fichier ASCII horodaté (compatible navigateurs / Azure).</summary>
    public static string FileName(string slug) => $"{slug}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

    public static FileContentResult FileResult(ControllerBase controller, string csvBody, string slug)
    {
        var fileName = FileName(slug);
        var bytes = ToUtf8BytesWithBom(csvBody);
        return controller.File(bytes, "text/csv; charset=utf-8", fileName);
    }
}
