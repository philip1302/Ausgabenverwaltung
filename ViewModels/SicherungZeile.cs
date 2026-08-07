using System;
using System.Globalization;
using Ausgabenverwaltung.Core.Backups;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine Zeile der Sicherungsliste. Zeitstempel und Groesse kommen fertig
/// aus Core (<see cref="BackupFile"/>, <see cref="BackupSizeText"/>).
///
/// Beobachtbar, weil das Ergebnis der Pruefung an der Zeile stehen bleibt
/// (<see cref="PruefungText"/>): eine gepruefte Sicherung soll sich von
/// einer ungeprueften unterscheiden, ohne dass die ganze Liste neu
/// aufgebaut wird - der Anwender prueft von oben nach unten und verloere
/// sonst bei jeder Zeile die Ergebnisse der vorigen.
/// </summary>
public sealed partial class SicherungZeile : ObservableObject
{
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    public SicherungZeile(BackupFile datei)
    {
        VollerPfad = datei.FullPath;
        Dateiname = datei.FileName;
        Zeitpunkt = datei.Timestamp;
        DatumText = datei.Timestamp.ToString("dd.MM.yyyy HH:mm", DeDe);
        GroesseText = BackupSizeText.Format(datei.SizeBytes);
    }

    /// <summary>Fuer die Pruefung - sie oeffnet die Datei selbst.</summary>
    public string VollerPfad { get; }

    public string Dateiname { get; }
    public DateTime Zeitpunkt { get; }
    public string DatumText { get; }
    public string GroesseText { get; }

    /// <summary>
    /// Das Merkmal nach einer Pruefung („geprüft ✓ · Schema 4 · 1 284
    /// Buchungen") - NULL, solange diese Zeile nicht geprueft wurde. Der
    /// Text stammt aus <see cref="BackupVerificationText"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PruefungSichtbar))]
    private string? _pruefungText;

    public bool PruefungSichtbar => PruefungText is not null;

    /// <summary>Ob die Pruefung etwas zu beanstanden hatte - faerbt das
    /// Merkmal, ersetzt es aber nicht (Regel 10 sinngemaess).</summary>
    [ObservableProperty]
    private bool _pruefungIstFehler;
}
