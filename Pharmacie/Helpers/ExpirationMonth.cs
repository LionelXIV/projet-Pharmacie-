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
}
