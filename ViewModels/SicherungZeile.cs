using System.Globalization;
using Ausgabenverwaltung.Core.Backups;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine Zeile der Sicherungsliste. Reine Anzeige - Zeitstempel und
/// Groesse kommen fertig aus Core
/// (<see cref="BackupFile"/>, <see cref="BackupSizeText"/>).
/// </summary>
public sealed class SicherungZeile
{
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    public SicherungZeile(BackupFile datei)
    {
        Dateiname = datei.FileName;
        DatumText = datei.Timestamp.ToString("dd.MM.yyyy HH:mm", DeDe);
        GroesseText = BackupSizeText.Format(datei.SizeBytes);
    }

    public string Dateiname { get; }
    public string DatumText { get; }
    public string GroesseText { get; }
}
