using System.Globalization;

namespace Ausgabenverwaltung.Core.Formatting;

/// <summary>
/// Zentrale Umwandlung von <see cref="DateOnly"/> in das in der DB
/// geforderte TEXT-Format 'YYYY-MM-DD'. Noetig, weil Dapper DateOnly
/// nicht von sich aus als Parameter serialisieren oder aus einer
/// SQLite-Spalte lesen kann (wirft NotSupportedException).
/// </summary>
public static class IsoDate
{
    private const string Format = "yyyy-MM-dd";

    public static string ToDateText(DateOnly date)
    {
        return date.ToString(Format, CultureInfo.InvariantCulture);
    }

    public static DateOnly ParseDate(string text)
    {
        return DateOnly.ParseExact(text, Format, CultureInfo.InvariantCulture);
    }
}
