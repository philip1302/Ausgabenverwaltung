using System;
using System.Collections.ObjectModel;
using System.IO;
using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Logging;
using Ausgabenverwaltung.Core.Settings;
using Ausgabenverwaltung.Core.Startup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Verwaltungsbereich "Datensicherung".
///
/// Die Seite beantwortet EINE Frage - "bin ich abgesichert?" - und stellt
/// alles andere dahinter. Deshalb gibt es genau eine Statusaussage
/// (<see cref="ZustandUeberschrift"/>/<see cref="ZustandText"/>, berechnet
/// in <see cref="BackupHealth"/>), darunter je Ziel eine schlanke Zeile
/// (<see cref="ZielZeile"/>) und genau einen Meldungsweg
/// (<see cref="MeldungText"/>). Vorher trugen Banner, Meldungsband, zwei
/// Statustexte und zwei verschieden gebaute Fehlerkaesten Teilaussagen
/// nebeneinander, die der Anwender selbst verrechnen musste.
///
/// Gesichert, aufgeraeumt, geprueft und beurteilt wird nichts hier: das
/// steckt in Core (<see cref="BackupService"/>, <see cref="BackupHealth"/>,
/// <see cref="BackupVerification"/>, <see cref="BackupRetention"/>,
/// <see cref="BackupTarget"/>) - Regel 7. Hier entstehen nur Anzeigetexte
/// und die Verdrahtung.
///
/// Wiederherstellen gibt es bewusst NICHT als Funktion (siehe
/// <see cref="WiederherstellungHinweis"/>).
/// </summary>
public sealed partial class DatensicherungViewModel : ViewModelBase
{
    private readonly BackupService _backupService;
    private readonly AppSettingsStore _settingsStore;

    /// <summary>
    /// Der letzte Sicherungslauf dieser Sitzung - Grundlage der beiden
    /// Problemtexte und des Gesamtzustands. NULL, solange keiner lief.
    /// </summary>
    private BackupResult? _letzterLauf;

    // Fuer "Rueckgaengig" nach dem Entfernen des zweiten Ziels. Beides
    // gehoert zusammen: der Zeitstempel ohne Pfad hat keine Aussage.
    private string? _entfernterZielPfad;
    private DateTime? _entfernterZielZeitstempel;

    public DatensicherungViewModel(
        BackupService backupService,
        AppSettingsStore settingsStore,
        StartupResult startupResult)
    {
        _backupService = backupService;
        _settingsStore = settingsStore;

        DatenbankPfad = startupResult.DatabaseFilePath;
        SicherungsordnerPfad = backupService.PrimaryFolderPath;
        ProtokollordnerPfad = AppLog.Current.FolderPath;

        // Eine beim Start gescheiterte Sicherung verhindert den Start
        // nicht, darf aber auch nicht mit dem Hinweisband verschwinden,
        // das der Anwender wegklickt. Sie steht in der Zielzeile, bis eine
        // Sicherung gelingt - das ist der Ort, an dem jemand nachsieht,
        // wenn er wissen will, ob gesichert wird.
        _letzterLauf = startupResult.Backup;

        Aktualisiere();
    }

    public ObservableCollection<SicherungZeile> Sicherungen { get; } = new();

    /// <summary>Pfad der aktiven Datenbankdatei - fuer den Kopieren-Knopf.</summary>
    public string DatenbankPfad { get; }

    /// <summary>
    /// Der Ordner, in dem die aktive Datenbank liegt - fuer den Knopf, der
    /// ihn im Dateimanager oeffnet. Abgeleitet aus dem tatsaechlich
    /// benutzten Dateipfad und nicht aus AppPaths neu berechnet: geoeffnet
    /// werden soll der Ordner der Datei, die gerade wirklich offen ist.
    ///
    /// NULL, wenn sich aus dem Pfad kein Ordner ergibt (etwa bei einem
    /// blossen Dateinamen ohne Verzeichnis).
    /// </summary>
    public string? DatenbankOrdnerPfad => Path.GetDirectoryName(DatenbankPfad);

    /// <summary>Ziel 1, fest und immer aktiv.</summary>
    public string SicherungsordnerPfad { get; }

    /// <summary>
    /// Der Protokollordner - NULL, wenn gerade nicht protokolliert wird.
    /// Steht hier, weil dies die Stelle ist, an der jemand nachsieht, wenn
    /// eine Sicherung nicht geklappt hat.
    /// </summary>
    public string? ProtokollordnerPfad { get; }

    public bool ProtokollordnerVerfuegbar => ProtokollordnerPfad is not null;

    // ================= Zustandskarte =================

    /// <summary>
    /// Der Gesamtzustand - die einzige Statusaussage der Seite.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZustandGut))]
    [NotifyPropertyChangedFor(nameof(ZustandEingeschraenkt))]
    [NotifyPropertyChangedFor(nameof(ZustandSchlecht))]
    private BackupHealthLevel _zustand = BackupHealthLevel.NichtGesichert;

    public bool ZustandGut => Zustand == BackupHealthLevel.Gesichert;
    public bool ZustandEingeschraenkt => Zustand == BackupHealthLevel.Eingeschraenkt;
    public bool ZustandSchlecht => Zustand == BackupHealthLevel.NichtGesichert;

    [ObservableProperty]
    private string _zustandUeberschrift = string.Empty;

    /// <summary>Wann zuletzt wohin gesichert wurde - ein Satz ueber
    /// beide Ziele.</summary>
    [ObservableProperty]
    private string _zustandZeitangabe = string.Empty;

    /// <summary>Was der Zustand bedeutet und was zu tun ist.</summary>
    [ObservableProperty]
    private string _zustandText = string.Empty;

    // ================= Ziele =================

    [ObservableProperty]
    private ZielZeile _ziel1 = LeereZeile("Dieser Rechner");

    [ObservableProperty]
    private ZielZeile _ziel2 = LeereZeile("Zusätzlicher Ordner");

    // ================= Sicherungsliste =================

    [ObservableProperty]
    private bool _keineSicherungen;

    [ObservableProperty]
    private string _anzahlText = string.Empty;

    // ================= Meldungen =================
    //
    // Der EINZIGE Meldungsweg der Seite. Dauerhafte Zustaende stehen in
    // der Zustandskarte bzw. der Zielzeile; hier steht ausschliesslich,
    // was gerade auf eine Handlung des Anwenders hin passiert ist.

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MeldungSichtbar))]
    private string? _meldungText;

    public bool MeldungSichtbar => MeldungText is not null;

    [ObservableProperty]
    private bool _meldungIstFehler;

    // ================= Rueckgaengig =================

    /// <summary>
    /// Das Band nach "Ziel entfernen". Reibung passend zum
    /// Schadensausmass: es geht nichts verloren, ein Bestaetigungsdialog
    /// waere zu viel - umkehrbar machen schlaegt nachfragen. Das Band
    /// bleibt fuer die Dauer der Sitzung stehen.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RueckgaengigSichtbar))]
    private string? _rueckgaengigText;

    public bool RueckgaengigSichtbar => RueckgaengigText is not null;

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

    /// <summary>
    /// Laedt Einstellungen und Dateiliste neu und beurteilt den Zustand
    /// neu. Oeffentlich, weil der Bereich ein DI-Singleton ist und
    /// zwischenzeitlich - etwa durch den Menuepunkt "Sicherung jetzt" -
    /// eine Sicherung entstanden sein kann.
    /// </summary>
    public void Aktualisiere()
    {
        var einstellungen = _settingsStore.Load();
        var dateien = _backupService.ListBackups();

        Sicherungen.Clear();
        foreach (var datei in dateien)
        {
            Sicherungen.Add(new SicherungZeile(datei));
        }

        KeineSicherungen = Sicherungen.Count == 0;
        AnzahlText = Sicherungen.Count == 1 ? "1 Sicherung" : $"{Sicherungen.Count} Sicherungen";

        var ziel2Pfad = einstellungen.ExternalFolderPath;
        var ziel2Eingerichtet = !string.IsNullOrWhiteSpace(ziel2Pfad);

        // Lokale Zeit fuer Ziel 1 (der Zeitstempel stammt aus dem
        // Dateinamen und ist lokal), UTC fuer Ziel 2 (dort steht ein
        // gespeicherter Zeitstempel, Regel 3).
        var jetztLokal = DateTime.Now;
        var jetztUtc = DateTime.UtcNow;

        var gesundheit = BackupHealth.Evaluate(
            _letzterLauf,
            dateien.Count,
            dateien.Count == 0 ? null : dateien[0].Timestamp,
            ziel2Eingerichtet,
            einstellungen.LastExternalBackupUtc,
            jetztUtc);

        Zustand = gesundheit.Level;
        ZustandUeberschrift = BackupHealthText.Ueberschrift(gesundheit.Level);
        ZustandText = BackupHealthText.Erklaerung(gesundheit.Level);
        ZustandZeitangabe = BackupHealthText.Zeitangabe(gesundheit, jetztLokal, jetztUtc);

        Ziel1 = new ZielZeile
        {
            Name = "Dieser Rechner",
            Punkt = gesundheit.PrimaryFailed || dateien.Count == 0
                ? BackupHealthLevel.NichtGesichert
                : BackupHealthLevel.Gesichert,
            ZeitpunktText = Sicherungen.Count == 0
                ? "noch keine Sicherung"
                : Sicherungen[0].DatumText + " Uhr",
            Pfad = SicherungsordnerPfad,
            IstEingerichtet = true,
            ProblemText = _letzterLauf?.Primary == BackupOutcome.Failed
                ? FileErrorText.ForBackup(_letzterLauf.PrimaryProblem)
                : null,
        };

        Ziel2 = new ZielZeile
        {
            Name = "Zusätzlicher Ordner",
            Punkt = !ziel2Eingerichtet || gesundheit.ExternalFailed || gesundheit.ExternalStale
                ? BackupHealthLevel.Eingeschraenkt
                : BackupHealthLevel.Gesichert,
            ZeitpunktText = ziel2Eingerichtet
                ? ExternalBackupAge.ToText(einstellungen.LastExternalBackupUtc, jetztUtc)
                : "nicht eingerichtet",
            Pfad = ziel2Pfad,
            IstEingerichtet = ziel2Eingerichtet,
            ProblemText = ziel2Eingerichtet && _letzterLauf?.External == BackupOutcome.Failed
                ? FileErrorText.ForExternalBackup(_letzterLauf.ExternalProblem, ziel2Pfad!)
                : null,
        };
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
        _letzterLauf = _backupService.RunNow(DateTime.Now);

        Aktualisiere();
        Melde(_letzterLauf);
    }

    /// <summary>
    /// Prueft eine vorhandene Sicherung: entpacken, oeffnen, durchzaehlen.
    /// Eine Sicherung ist nur so viel wert wie ihre Wiederherstellbarkeit -
    /// eine Liste von Dateinamen belegt gar nichts.
    /// </summary>
    [RelayCommand]
    private void SicherungPruefen(SicherungZeile? zeile)
    {
        if (zeile is null)
        {
            return;
        }

        var ergebnis = BackupVerification.Verify(zeile.VollerPfad);

        AppLog.Current.Info(
            LogEvents.BackupVerified(zeile.Dateiname, ergebnis.IsReadable, ergebnis.SchemaVersion));

        // Das Merkmal bleibt an der Zeile stehen, auch wenn das Band
        // weggeklickt wird - sonst weiss der Anwender nach der dritten
        // Pruefung nicht mehr, welche Datei er schon angesehen hat.
        zeile.PruefungText = BackupVerificationText.Merkmal(ergebnis);
        zeile.PruefungIstFehler = !ergebnis.IsReadable || ergebnis.IsSchemaTooNew;

        if (!ergebnis.IsReadable)
        {
            MeldeFehler(FileErrorText.ForBackupVerification(ergebnis.Problem, zeile.Dateiname));
            return;
        }

        if (ergebnis.IsSchemaTooNew)
        {
            MeldeFehler(FileErrorText.ForBackupFromNewerVersion(
                zeile.Dateiname,
                ergebnis.SchemaVersion ?? 0,
                DatabaseInitializer.ExpectedSchemaVersion));
            return;
        }

        MeldeErfolg(
            $"Die Sicherung „{zeile.Dateiname}“ ist lesbar und vollständig: "
            + $"{BackupVerificationText.Merkmal(ergebnis)}.");
    }

    /// <summary>
    /// Uebernimmt einen vom Anwender gewaehlten Ordner als Ziel 2 - erst
    /// nach einer bestandenen Schreibprobe. Wird von der Ansicht nach dem
    /// Ordnerdialog aufgerufen; die Dateiauswahl selbst ist reine
    /// Bedienmechanik und bleibt dort.
    /// </summary>
    public void SetzeZweitesZiel(string ordnerPfad)
    {
        var probe = BackupTarget.Check(ordnerPfad);
        if (!probe.IsWritable)
        {
            MeldeFehler(FileErrorText.ForBackupTargetChoice(probe.Problem));
            return;
        }

        _settingsStore.Save(_settingsStore.Load() with { ExternalFolderPath = ordnerPfad });

        // Ein neu gewaehltes Ziel macht das Rueckgaengig-Angebot
        // gegenstandslos - es zeigte sonst auf einen Pfad, der gerade
        // bewusst ersetzt wurde.
        VergissEntferntesZiel();

        Aktualisiere();

        MeldeErfolg(
            $"Zweites Ziel gespeichert: {ordnerPfad}\n"
            + "Es wird ab der nächsten Sicherung mitbeschrieben.");
    }

    [RelayCommand]
    private void ZweitesZielEntfernen()
    {
        var einstellungen = _settingsStore.Load();

        _entfernterZielPfad = einstellungen.ExternalFolderPath;
        _entfernterZielZeitstempel = einstellungen.LastExternalBackupUtc;

        // Der Zeitstempel wird mit entfernt: ohne Ziel hat "letzte externe
        // Sicherung vor 3 Tagen" keine Aussage mehr, und bliebe er stehen,
        // wuerde er ein spaeter gewaehltes neues Ziel falsch beschreiben.
        _settingsStore.Save(einstellungen with
        {
            ExternalFolderPath = null,
            LastExternalBackupUtc = null,
        });

        Aktualisiere();

        RueckgaengigText = "Das zweite Ziel wurde entfernt. Gesichert wird weiterhin "
                         + "auf diesem Rechner.";
    }

    [RelayCommand]
    private void ZweitesZielWiederherstellen()
    {
        if (_entfernterZielPfad is null)
        {
            return;
        }

        _settingsStore.Save(_settingsStore.Load() with
        {
            ExternalFolderPath = _entfernterZielPfad,
            LastExternalBackupUtc = _entfernterZielZeitstempel,
        });

        var pfad = _entfernterZielPfad;
        VergissEntferntesZiel();

        Aktualisiere();

        MeldeErfolg($"Das zweite Ziel gilt wieder: {pfad}");
    }

    [RelayCommand]
    private void RueckgaengigSchliessen() => VergissEntferntesZiel();

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

    private void VergissEntferntesZiel()
    {
        _entfernterZielPfad = null;
        _entfernterZielZeitstempel = null;
        RueckgaengigText = null;
    }

    private void Melde(BackupResult ergebnis)
    {
        if (ergebnis.Primary == BackupOutcome.Failed)
        {
            // Der ausfuehrliche Text steht bereits in der Zielzeile; das
            // Band sagt nur, dass der eben ausgeloeste Lauf gescheitert
            // ist, und verweist dorthin.
            MeldeFehler(FileErrorText.ForBackup(ergebnis.PrimaryProblem));
            return;
        }

        var text = $"Sicherung erstellt: {ergebnis.FileName}";

        MeldungIstFehler = false;
        MeldungText = ergebnis.External switch
        {
            BackupOutcome.Succeeded => text + "\nAuch in das zweite Ziel kopiert.",

            // Kein Fehlerton: Ziel 1 hat funktioniert, die Daten sind
            // gesichert. Der Hinweis sagt nur, dass die zweite Kopie fehlt,
            // und wo der Grund steht.
            BackupOutcome.Failed => text
                + "\nDie zweite Kopie ist nicht angekommen — der Grund steht oben "
                + "in der Zeile „Zusätzlicher Ordner“.",

            _ => text,
        };
    }

    // Vor dem ersten Aktualisiere(): eine Zeile, die nichts behauptet.
    private static ZielZeile LeereZeile(string name) => new()
    {
        Name = name,
        Punkt = BackupHealthLevel.NichtGesichert,
        ZeitpunktText = string.Empty,
    };
}
