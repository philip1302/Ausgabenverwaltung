using System.Globalization;

namespace Ausgabenverwaltung.Core.Formatting;

/// <summary>
/// Zentrale Umwandlung von UTC-Zeitstempeln in das in der DB geforderte
/// TEXT-Format 'YYYY-MM-DDTHH:MM:SSZ'. Noetig, weil der ADO.NET-Treiber
/// (Microsoft.Data.Sqlite) DateTime-Parameter standardmaessig in einem
/// anderen Format serialisiert ("yyyy-MM-dd HH:mm:ss.fffffff") und damit
/// gegen das vorgeschriebene Datumsformat verstossen wuerde.
/// </summary>
public static class IsoDateTime
{
    private const string Format = "yyyy-MM-ddTHH:mm:ssZ";

    public static string ToUtcText(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Zeitstempel muss DateTimeKind.Utc sein.", nameof(utc));
        }

        return utc.ToString(Format, CultureInfo.InvariantCulture);
    }

    public static DateTime ParseUtc(string text)
    {
        return DateTime.ParseExact(
            text, Format, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
    }
}
