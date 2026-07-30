using System.Globalization;

namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Dateigroesse als deutscher Anzeigetext. Steht in Core, weil eine
/// Rundung mit Schwellenwerten pruefbar ist und pruefbare Dinge nicht in
/// ViewModels gehoeren (Regel 7).
/// </summary>
public static class BackupSizeText
{
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    private const long Kilobyte = 1024;
    private const long Megabyte = 1024 * Kilobyte;

    public static string Format(long bytes)
    {
        if (bytes < Kilobyte)
        {
            return $"{bytes} Bytes";
        }

        if (bytes < Megabyte)
        {
            // Kilobyte ohne Nachkommastelle: bei Sicherungen dieser
            // Groessenordnung interessiert die Zahl nur als Hausnummer.
            return $"{((double)bytes / Kilobyte).ToString("N0", DeDe)} KB";
        }

        return $"{((double)bytes / Megabyte).ToString("N1", DeDe)} MB";
    }
}
