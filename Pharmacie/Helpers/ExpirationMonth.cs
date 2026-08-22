namespace Pharmacie.Helpers;

/// <summary>Péremption métier : mois/année uniquement (stockée au dernier jour du mois).</summary>
public static class ExpirationMonth
{
    public const string DisplayFormat = "MM/yyyy";
    public const string InputFormat = "yyyy-MM";

    public static string Format(DateTime? date) =>
        date is { } d && d != default ? d.ToString(DisplayFormat) : "—";

    public static string Format(DateTime date) =>
        date == default ? "—" : date.ToString(DisplayFormat);

    public static string ToInputValue(DateTime? date) =>
        date is { } d && d != default ? d.ToString(InputFormat) : "";

    public static DateTime EndOfMonth(DateTime date)
    {
        var last = DateTime.DaysInMonth(date.Year, date.Month);
        return new DateTime(date.Year, date.Month, last);
    }

    public static DateTime? EndOfMonth(DateTime? date) =>
        date is { } d && d != default ? EndOfMonth(d) : null;

    /// <summary>Accepte <c>yyyy-MM</c> (dernier jour du mois) ou une date/heure complète (inchangée).</summary>
    public static bool TryParse(string? value, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var raw = value.Trim();
        if (raw.Length == 7 && raw[4] == '-'
            && int.TryParse(raw.AsSpan(0, 4), out var year)
            && int.TryParse(raw.AsSpan(5, 2), out var month)
            && year is >= 2000 and <= 2100
            && month is >= 1 and <= 12)
        {
            date = EndOfMonth(new DateTime(year, month, 1));
            return true;
        }

        if (DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var parsed)
            || DateTime.TryParse(raw, out parsed))
        {
            date = parsed;
            return true;
        }

        return false;
    }
}
