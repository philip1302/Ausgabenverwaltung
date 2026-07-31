using System.Globalization;

namespace Ausgabenverwaltung.Core.Logging;

/// <summary>
/// Bildet und liest den Dateinamen einer Protokolldatei:
/// <c>ausgabenverwaltung_2026-07-31.log</c>.
///
/// Eine Datei je Kalendertag. Der Tag im Namen ist der einzige Zustand der
/// Protokollierung - es gibt keine Merkdatei, die festhaelt, welche Datei
/// gerade die aktuelle ist. Wer im Ordner von Hand aufraeumt, bringt
/// dadurch nichts durcheinander.
///
/// Bewusst LOKALE Zeit im Namen, obwohl die Zeitstempel IN der Datei UTC
/// sind (Regel 3): der Name wird von Menschen gelesen, die einen Absturz
/// "von gestern Abend" suchen. Die Zeitstempel darin bleiben vergleichbar.
/// </summary>
public static class LogFileName
{
    private const string Prefix = "ausgabenverwaltung_";
    private const string Extension = ".log";

    // Sortiert alphabetisch = chronologisch, wie ueberall sonst.
    private const string DateFormat = "yyyy-MM-dd";

    public static string Create(DateOnly localDate)
    {
        return Prefix
            + localDate.ToString(DateFormat, CultureInfo.InvariantCulture)
            + Extension;
    }

    /// <summary>
    /// Liest den Tag aus einem Dateinamen. Liefert false fuer alles, was
    /// nicht exakt dem Muster entspricht - fremde Dateien im
    /// Protokollordner werden dadurch weder gezaehlt noch je geloescht.
    /// </summary>
    public static bool TryParseDate(string fileName, out DateOnly localDate)
    {
        localDate = default;

        if (!fileName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            || !fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var datePart = fileName[Prefix.Length..^Extension.Length];

        return DateOnly.TryParseExact(
            datePart, DateFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out localDate);
    }
}
