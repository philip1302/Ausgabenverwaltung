using System.Globalization;

namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Bildet und liest den Dateinamen einer Sicherung:
/// <c>ausgaben_2026-07-30_1842.zip</c>.
///
/// Der Zeitstempel ist der einzige Zustand der Sicherung - es gibt keine
/// Merkdatei, die festhaelt, wann zuletzt gesichert wurde. "Heute schon
/// gesichert?" und die gestaffelte Aufbewahrung lesen beide nur die
/// vorhandenen Dateinamen. Loescht jemand von Hand im Ordner, stimmt das
/// Bild dadurch sofort wieder.
///
/// Bewusst LOKALE Zeit, nicht UTC: der Name wird von Menschen gelesen und
/// soll zu dem Tag passen, an dem der Anwender das Programm gestartet hat
/// (Regel 3 gilt fuer gespeicherte Zeitstempel in der Datenbank).
/// </summary>
public static class BackupFileName
{
    private const string Prefix = "ausgaben_";
    private const string Extension = ".zip";

    // Sortiert alphabetisch = chronologisch, wie das Datumsformat der DB.
    private const string TimestampFormat = "yyyy-MM-dd_HHmm";

    public static string Create(DateTime localTimestamp)
    {
        return Prefix
            + localTimestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture)
            + Extension;
    }

    /// <summary>
    /// Liest den Zeitstempel aus einem Dateinamen. Liefert false fuer alles,
    /// was nicht exakt dem Muster entspricht - fremde Dateien im
    /// Sicherungsordner (etwa in einem selbst gewaehlten zweiten Ziel)
    /// werden dadurch weder gezaehlt noch je geloescht.
    /// </summary>
    public static bool TryParseTimestamp(string fileName, out DateTime localTimestamp)
    {
        localTimestamp = default;

        if (!fileName.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            || !fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var timestampPart = fileName[Prefix.Length..^Extension.Length];

        return DateTime.TryParseExact(
            timestampPart, TimestampFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out localTimestamp);
    }
}
