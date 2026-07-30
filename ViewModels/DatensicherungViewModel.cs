using System;
using System.Collections.ObjectModel;
using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Settings;
using Ausgabenverwaltung.Core.Startup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Verwaltungsbereich "Datensicherung": Zustand beider Ziele, das zweite
/// Ziel waehlen oder entfernen, von Hand sichern, vorhandene Sicherungen
/// ansehen.
///
/// Gesichert, aufgeraeumt und geprueft wird nichts hier: das steckt in
/// Core (<see cref="BackupService"/>, <see cref="BackupRetention"/>,
/// <see cref="BackupTarget"/>, <see cref="ExternalBackupAge"/>) - Regel 7.
/// Hier entstehen nur Anzeigetexte.
///
/// Wiederherstellen gibt es bewusst NICHT als Funktion (siehe
/// <see cref="WiederherstellungHinweis"/>).
/// </summary>
public sealed partial class DatensicherungViewModel : ViewModelBase
{
    private readonly BackupService _backupService;
    private readonly AppSettingsStore _settingsStore;

    public DatensicherungViewModel(
        BackupService backupService,
        AppSettingsStore settingsStore,
        StartupResult startupResult)
    {
        _backupService = backupService;
        _settingsStore = settingsStore;

        DatenbankPfad = startupResult.DatabaseFilePath;
        SicherungsordnerPfad = backupService.PrimaryFolderPath;

        Aktualisiere();
    }

    public ObservableCollection<SicherungZeile> Sicherungen { get; } = new();

    /// <summary>Pfad der aktiven Datenbankdatei - fuer den Kopieren-Knopf.</summary>
    public string DatenbankPfad { get; }

    /// <summary>Ziel 1, fest und immer aktiv.</summary>
    public string SicherungsordnerPfad { get; }

    public string WiederherstellungHinweis =>
        "Wiederherstellen geschieht bewusst von Hand und nicht aus der Anwendung heraus: "
        + "Eine Wiederherstellungsfunktion, die im Fehlerfall die falsche Datei überschreibt, "
        + "richtet mehr Schaden an als die zwei Handgriffe im Explorer.\n\n"
        + "1. Anwendung schließen.\n"
        + "2. Gewünschte ZIP-Datei im Sicherungsordner entpacken — darin liegt eine Datei "
        + "namens „ausgaben.db“.\n"
        + "3. Diese Datei an den Ort der aktiven Datenbank kopieren und die dortige Datei "
        + "ersetzen. Vorher lohnt es sich, die bisherige Datei umzubenennen statt sie zu "
        + "überschreiben.\n"
        + "4. Anwendung wieder starten.";

    [ObservableProperty]
    private string _ziel1StatusText = string.Empty;

    [ObservableProperty]
    private string _ziel2Pfad = string.Empty;

    [ObservableProperty]
    private bool _ziel2Eingerichtet;

    [ObservableProperty]
    private string _ziel2StatusText = string.Empty;

    /// <summary>
    /// Ob die externe Sicherung dezent hervorzuheben ist - die Schwelle
    /// von 14 Tagen steht in <see cref="ExternalBackupAge"/>.
    /// </summary>
    [ObservableProperty]
    private bool _ziel2Veraltet;

    [ObservableProperty]
    private bool _keineSicherungen;

    [ObservableProperty]
    private string _anzahlText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MeldungSichtbar))]
    private string? _meldungText;

    public bool MeldungSichtbar => MeldungText is not null;

    [ObservableProperty]
    private bool _meldungIstFehler;

    /// <summary>
    /// Laedt Einstellungen und Dateiliste neu. Oeffentlich, weil der
    /// Bereich ein DI-Singleton ist und zwischenzeitlich - etwa durch den
    /// Menuepunkt "Sicherung jetzt" - eine Sicherung entstanden sein kann.
    /// </summary>
    public void Aktualisiere()
    {
        var einstellungen = _settingsStore.Load();

        Sicherungen.Clear();
        foreach (var datei in _backupService.ListBackups())
        {
            Sicherungen.Add(new SicherungZeile(datei));
        }

        KeineSicherungen = Sicherungen.Count == 0;
        AnzahlText = Sicherungen.Count == 1 ? "1 Sicherung" : $"{Sicherungen.Count} Sicherungen";

        Ziel1StatusText = Sicherungen.Count == 0
            ? "Noch keine Sicherung vorhanden."
            : $"Letzte Sicherung: {Sicherungen[0].DatumText} Uhr ({Sicherungen[0].GroesseText}).";

        Ziel2Eingerichtet = !string.IsNullOrWhiteSpace(einstellungen.ExternalFolderPath);
        Ziel2Pfad = einstellungen.ExternalFolderPath ?? string.Empty;

        if (!Ziel2Eingerichtet)
        {
            Ziel2StatusText = "Nicht eingerichtet — die Sicherungen liegen nur auf diesem Rechner.";
            Ziel2Veraltet = false;
            return;
        }

        var jetzt = DateTime.UtcNow;
        Ziel2StatusText =
            $"Letzte externe Sicherung: {ExternalBackupAge.ToText(einstellungen.LastExternalBackupUtc, jetzt)}.";
        Ziel2Veraltet = ExternalBackupAge.IsStale(einstellungen.LastExternalBackupUtc, jetzt);
    }

    /// <summary>
    /// Sichert sofort, ohne Ruecksicht auf die Tagesbegrenzung. Der Weg
    /// fuer "ich raeume jetzt gleich groesser auf".
    /// </summary>
    [RelayCommand]
    public void SicherungJetzt()
    {
        // Lokale Zeit, weil der Dateiname zum Kalendertag des Anwenders
        // passen soll (Regel 3 betrifft gespeicherte Zeitstempel).
        var ergebnis = _backupService.RunNow(DateTime.Now);

        Aktualisiere();
        Melde(ergebnis);
    }

    /// <summary>
    /// Uebernimmt einen vom Anwender gewaehlten Ordner als Ziel 2 - erst
    /// nach einer bestandenen Schreibprobe. Wird von der Ansicht nach dem
    /// Ordnerdialog aufgerufen; die Dateiauswahl selbst ist reine
    /// Bedienmechanik und bleibt dort.
    /// </summary>
    public void SetzeZweitesZiel(string ordnerPfad)
    {
        var fehler = BackupTarget.TestWritable(ordnerPfad);
        if (fehler is not null)
        {
            MeldungIstFehler = true;
            MeldungText =
                $"In diesen Ordner lässt sich nicht schreiben, er wurde nicht übernommen:\n{fehler}";
            return;
        }

        _settingsStore.Save(_settingsStore.Load() with { ExternalFolderPath = ordnerPfad });

        Aktualisiere();

        MeldungIstFehler = false;
        MeldungText =
            $"Zweites Ziel gespeichert: {ordnerPfad}\n"
            + "Es wird ab der nächsten Sicherung mitbeschrieben.";
    }

    [RelayCommand]
    private void ZweitesZielEntfernen()
    {
        // Der Zeitstempel wird mit entfernt: ohne Ziel hat "letzte externe
        // Sicherung vor 3 Tagen" keine Aussage mehr, und bliebe er stehen,
        // wuerde er ein spaeter gewaehltes neues Ziel falsch beschreiben.
        _settingsStore.Save(_settingsStore.Load() with
        {
            ExternalFolderPath = null,
            LastExternalBackupUtc = null,
        });

        Aktualisiere();

        MeldungIstFehler = false;
        MeldungText = "Das zweite Ziel wurde entfernt. Gesichert wird weiterhin auf diesem Rechner.";
    }

    [RelayCommand]
    private void MeldungSchliessen() => MeldungText = null;

    /// <summary>
    /// Setzt eine Meldung von aussen - fuer Fehler, die nur in der
    /// Oberflaeche entstehen koennen (Ordnerdialog, Explorer, Zwischenablage).
    /// </summary>
    public void MeldeFehler(string text)
    {
        MeldungIstFehler = true;
        MeldungText = text;
    }

    public void MeldeErfolg(string text)
    {
        MeldungIstFehler = false;
        MeldungText = text;
    }

    private void Melde(BackupResult ergebnis)
    {
        if (ergebnis.Primary == BackupOutcome.Failed)
        {
            MeldungIstFehler = true;
            MeldungText = $"Die Sicherung ist fehlgeschlagen:\n{ergebnis.PrimaryError}";
            return;
        }

        MeldungIstFehler = false;

        var text = $"Sicherung erstellt: {ergebnis.FileName}";

        MeldungText = ergebnis.External switch
        {
            BackupOutcome.Succeeded => text + "\nAuch in das zweite Ziel kopiert.",

            // Kein Fehlerton: Ziel 1 hat funktioniert, die Daten sind
            // gesichert. Der Hinweis sagt nur, dass die zweite Kopie fehlt.
            BackupOutcome.Failed =>
                text + $"\nDas zweite Ziel war nicht erreichbar: {ergebnis.ExternalError}",

            _ => text,
        };
    }
}
