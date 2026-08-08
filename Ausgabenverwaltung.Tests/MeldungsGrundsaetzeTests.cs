using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Startup;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Grundsaetze, die fuer JEDE Meldung gelten sollen - hier einmal
/// gegen alle Texte gehalten, statt sie in jedem einzelnen Test zu
/// wiederholen:
///
/// - sagen, was passiert ist, was das fuer die Daten bedeutet und was der
///   Anwender tun kann,
/// - keine technischen Begriffe, keine Ausnahmenamen, keine Aufrufstapel
///   im Haupttext,
/// - niemals eine Meldung, die nur "Ein Fehler ist aufgetreten" sagt,
/// - Deutsch, ganze Saetze.
///
/// Solche Tests sind ungewoehnlich, weil sie Sprache pruefen statt
/// Verhalten. Sie stehen hier trotzdem: die Texte sind das eigentliche
/// Erzeugnis dieser Fehlerbehandlung, und ohne eine Pruefung faellt es
/// niemandem auf, wenn spaeter ein "Fehler: SQLITE_BUSY" hineinrutscht.
/// </summary>
public class MeldungsGrundsaetzeTests
{
    // Woerter, die in einem Haupttext nichts verloren haben. Sie gehoeren
    // in den aufklappbaren Bereich und ins Protokoll.
    private static readonly string[] VerbotenImHaupttext =
    [
        "Exception", "SQLITE", "SqliteError", "HResult", "StackTrace",
        "at Ausgabenverwaltung", "null reference", "IOException",
    ];

    public static TheoryData<string, string> AlleHaupttexte()
    {
        var daten = new TheoryData<string, string>();

        void Nimm(string name, string text) => daten.Add(name, text);

        // ---- Dateisystem ----
        foreach (var problem in Enum.GetValues<StorageProblem>())
        {
            Nimm($"CsvExport/{problem}", FileErrorText.ForCsvExport(problem, "Auswertung.csv"));
            Nimm($"Sicherung/{problem}", FileErrorText.ForBackup(problem));
            Nimm($"Ziel2/{problem}", FileErrorText.ForExternalBackup(problem, @"D:\Stick"));
            Nimm($"Zielwahl/{problem}", FileErrorText.ForBackupTargetChoice(problem));
            Nimm($"Pruefung/{problem}", FileErrorText.ForBackupVerification(problem, "ausgaben-2026-08-07.zip"));
            Nimm($"Schreibfehler/{problem}", DatabaseErrorText.WriteFailed(problem));
            Nimm($"Wiederherstellen/{problem}", FileErrorText.ForRestore(problem, "ausgaben-2026-08-07.zip"));
            Nimm($"WiederherstellenKopie/{problem}", FileErrorText.ForRestoreSafetyCopy(problem));
        }

        Nimm("PruefungZuNeu", FileErrorText.ForBackupFromNewerVersion("ausgaben-2026-08-07.zip", 5, 4));

        // ---- Wiederherstellen ----
        // Keine Fehlertexte, aber Meldungen, die dieselbe Last tragen: sie
        // stehen vor der einzigen Aktion, die den ganzen Datenbestand
        // austauscht, und muessen deshalb genauso sagen, was mit den Daten
        // geschieht.
        Nimm("Wiederherstellen/Folgen", RestoreText.Folgen("ausgaben-2026-08-07.zip", 1284, 1190));
        Nimm("Wiederherstellen/FolgenGleich", RestoreText.Folgen("ausgaben-2026-08-07.zip", 12, 12));
        Nimm("Wiederherstellen/FolgenMehr", RestoreText.Folgen("ausgaben-2026-08-07.zip", 5, 40));
        Nimm("Wiederherstellen/Bereitgelegt", RestoreText.Bereitgelegt(
            "ausgaben-2026-08-07.zip", "ausgaben-vor-wiederherstellung-2026-08-08_1432.db"));
        Nimm("Wiederherstellen/Uebernommen", RestoreText.Uebernommen(
            "ausgaben-2026-08-07.zip", "ausgaben-vor-wiederherstellung-2026-08-08_1432.db"));
        Nimm("Wiederherstellen/Verworfen", RestoreText.Verworfen());

        // ---- Zustand der Datensicherung ----
        foreach (var stufe in Enum.GetValues<BackupHealthLevel>())
        {
            Nimm($"ZustandTitel/{stufe}", BackupHealthText.Ueberschrift(stufe));
            Nimm($"Zustand/{stufe}", BackupHealthText.Erklaerung(stufe));
        }

        // ---- Datenbank ----
        Nimm("Gesperrt", DatabaseErrorText.Locked(@"C:\Daten\ausgaben.db"));
        Nimm("Beschaedigt", DatabaseErrorText.Corrupt(@"C:\Daten\ausgaben.db", @"C:\Backups"));
        Nimm("SchemaZuNeu", DatabaseErrorText.SchemaTooNew(3, 2));
        Nimm("SchemaUnlesbar", DatabaseErrorText.SchemaUnreadable(@"C:\Daten\ausgaben.db", @"C:\Backups"));
        Nimm("MigrationGescheitert", DatabaseErrorText.MigrationFailed(1, 2, @"C:\Backups", "ausgaben.zip"));
        Nimm("MigrationOhneDateiname", DatabaseErrorText.MigrationFailed(1, 2, @"C:\Backups", null));

        // ---- Startabbrueche ----
        foreach (var (name, fehler) in AlleStartabbrueche())
        {
            Nimm($"Start/{name}", fehler.Message);
            Nimm($"StartTitel/{name}", fehler.Title);
        }

        // ---- Unerwarteter Fehler ----
        Nimm("Unerwartet/Anzeige", UnerwarteterBericht(new InvalidOperationException("x")).Message);
        Nimm("Unerwartet/Speichern", UnerwarteterBericht(
            new UnauthorizedAccessException("x")).Message);

        // ---- Selbstaktualisierung ----
        Nimm("Update/Bereitgelegt", UpdateText.Bereitgelegt("1.2.0"));
        Nimm("Update/Gescheitert", UpdateText.AustauschGescheitert("1.2.0"));
        Nimm("Update/WasIstNeu", UpdateText.WasIstNeuEinleitung("1.2.0"));
        Nimm("Update/NeustartGescheitert", UpdateText.NeustartGescheitert());

        foreach (var hindernis in Enum.GetValues<UpdateHindernis>())
        {
            // Alle drei Auspraegungen: ohne Anleitung (fuer dieses System
            // gibt es keine Datei), mit Programmdatei und mit Bundle. Die
            // Anleitung ist der laengste Teil des Textes - sie ungeprueft
            // zu lassen hiesse, gerade das Neue nicht zu pruefen.
            Nimm($"Update/Hinweis/{hindernis}", UpdateText.NurHinweis("1.2.0", hindernis));
            Nimm($"Update/HinweisDatei/{hindernis}", UpdateText.NurHinweis(
                "1.2.0", hindernis, "Ausgabenverwaltung-win-x64.zip"));
            Nimm($"Update/HinweisBundle/{hindernis}", UpdateText.NurHinweis(
                "1.2.0", hindernis, "Ausgabenverwaltung-osx-arm64.tar.gz", istBundle: true));
        }

        return daten;
    }

    [Theory]
    [MemberData(nameof(AlleHaupttexte))]
    public void Kein_Haupttext_enthaelt_technische_Begriffe(string name, string text)
    {
        foreach (var verboten in VerbotenImHaupttext)
        {
            Assert.False(
                text.Contains(verboten, StringComparison.OrdinalIgnoreCase),
                $"„{name}“ enthält den technischen Begriff „{verboten}“ im Haupttext.");
        }
    }

    [Theory]
    [MemberData(nameof(AlleHaupttexte))]
    public void Keine_Meldung_sagt_nur_dass_ein_Fehler_aufgetreten_ist(string name, string text)
    {
        Assert.False(
            text.Trim() == "Ein Fehler ist aufgetreten.",
            $"„{name}“ sagt nur, dass ein Fehler aufgetreten ist.");

        // Ueberschriften duerfen und sollen kurz sein - sie benennen den
        // Fall, erklaeren tut ihn der Haupttext darunter.
        if (IstUeberschrift(name))
        {
            return;
        }

        // Die Untergrenze fuer alles andere: eine Meldung muss mehr
        // hergeben als die Feststellung, dass etwas schiefging.
        Assert.True(text.Length > 40, $"„{name}“ ist zu kurz, um etwas zu erklären: „{text}“");
    }

    [Theory]
    [MemberData(nameof(AlleHaupttexte))]
    public void Jede_Meldung_besteht_aus_ganzen_Saetzen(string name, string text)
    {
        Assert.True(
            text.Contains('.') || text.Contains('?'),
            $"„{name}“ enthält keinen abgeschlossenen Satz.");

        // Ein ganzer Satz beginnt gross.
        Assert.True(
            char.IsUpper(text.TrimStart()[0]) || char.IsDigit(text.TrimStart()[0]),
            $"„{name}“ beginnt nicht mit einem Großbuchstaben.");
    }

    // Der Kern der Grundsaetze: was bedeutet das fuer meine Daten? Diese
    // Frage stellt sich jeder, der so eine Meldung liest, und sie muss
    // beantwortet dastehen.
    [Theory]
    [MemberData(nameof(AlleHaupttexte))]
    public void Jede_laengere_Meldung_sagt_etwas_ueber_den_Zustand_der_Daten(
        string name, string text)
    {
        // Ueberschriften sind absichtlich kurz und tragen die Aussage
        // nicht - sie sind hier ausgenommen.
        if (IstUeberschrift(name) || text.Length < 120)
        {
            return;
        }

        string[] aussagen =
        [
            "unverändert", "unberührt", "nicht betroffen", "nichts verändert",
            "zurückgenommen", "vollständig", "nicht gespeichert", "trotzdem angelegt",
            "gilt unverändert weiter", "es wurde nichts",
        ];

        Assert.True(
            aussagen.Any(aussage => text.Contains(aussage, StringComparison.OrdinalIgnoreCase)),
            $"„{name}“ sagt nicht, was mit den Daten ist.");
    }

    // ================= Aufklappbarer Bereich =================

    [Fact]
    public void Der_technische_Text_enthaelt_Typ_Meldung_und_Aufrufstapel()
    {
        var bericht = UnerwarteterBericht(WirfEcht());

        Assert.Contains("InvalidOperationException", bericht.Technical);
        Assert.Contains("Etwas ging schief", bericht.Technical);
        Assert.Contains(nameof(WirfEcht), bericht.Technical);

        // Und die Angaben, die eine Rueckfrage sofort beantworten.
        Assert.Contains("Version:", bericht.Technical);
        Assert.Contains("Zeitpunkt:", bericht.Technical);
    }

    [Fact]
    public void Der_Aufrufstapel_taucht_nicht_im_Haupttext_auf()
    {
        var bericht = UnerwarteterBericht(WirfEcht());

        Assert.DoesNotContain("   at ", bericht.Message);
        Assert.DoesNotContain(nameof(WirfEcht), bericht.Message);
    }

    // ================= Empfehlung =================

    [Fact]
    public void Ein_Anzeigefehler_laesst_das_Weiterarbeiten_zu()
    {
        var bericht = UnerwarteterBericht(new InvalidOperationException("Bindung kaputt"));

        Assert.Equal(ErrorRecommendation.Continue, bericht.Recommendation);
        Assert.Contains("können weiterarbeiten", bericht.Message);
    }

    [Fact]
    public void Ein_Speicherfehler_raet_zum_Neustart()
    {
        // "Die Anwendung soll sich nicht wegen eines Anzeigefehlers ganz
        // verabschieden, aber bei einem Fehler beim Speichern auch nicht
        // so tun, als waere nichts."
        var bericht = UnerwarteterBericht(new UnauthorizedAccessException("kein Zugriff"));

        Assert.Equal(ErrorRecommendation.Restart, bericht.Recommendation);
        Assert.Contains("beenden und neu zu starten", bericht.Message);
    }

    [Fact]
    public void Ohne_Protokoll_faellt_der_Verweis_darauf_weg_statt_ins_Leere_zu_fuehren()
    {
        var bericht = UnexpectedErrorText.Describe(
            "beim Testen", new InvalidOperationException("x"), logFolderPath: null, "1.0.0");

        Assert.Null(bericht.LogFolderPath);
        Assert.Contains("Protokoll wird gerade nicht geschrieben", bericht.Message);
    }

    [Fact]
    public void Mit_Protokoll_steht_der_Pfad_in_der_Meldung()
    {
        var bericht = UnexpectedErrorText.Describe(
            "beim Testen", new InvalidOperationException("x"), @"C:\Logs", "1.0.0");

        Assert.Equal(@"C:\Logs", bericht.LogFolderPath);
        Assert.Contains(@"C:\Logs", bericht.Message);
    }

    // ================= Hilfsmittel =================

    /// <summary>
    /// Ueberschriften benennen einen Fall, sie erklaeren ihn nicht: der
    /// Titel eines Startabbruchs ebenso wie die Ueberschrift der
    /// Zustandskarte in der Datensicherung. An sie gilt die Mindestlaenge
    /// nicht - der Text darunter traegt die Aussage, und dass ER etwas
    /// ueber die Daten sagt, prueft dieselbe Testreihe.
    /// </summary>
    private static bool IstUeberschrift(string name)
        => name.StartsWith("StartTitel/", StringComparison.Ordinal)
           || name.StartsWith("ZustandTitel/", StringComparison.Ordinal);

    private static ErrorReport UnerwarteterBericht(Exception ausnahme)
        => UnexpectedErrorText.Describe("beim Aufbau der Ansicht", ausnahme, @"C:\Logs", "1.0.0-test");

    private static IEnumerable<(string Name, StartupFailure Fehler)> AlleStartabbrueche()
    {
        StartupFailure Beschreibe(Exception ex) => StartupFailureText.Describe(
            ex, @"C:\Daten\ausgaben.db", @"C:\Backups", @"C:\Logs", "1.0.0-test");

        yield return ("Beschaedigt", Beschreibe(new DatabaseCorruptException(@"C:\x.db", "page 3")));
        yield return ("Unlesbar", Beschreibe(new SchemaVersionUnreadableException()));
        yield return ("ZuNeu", Beschreibe(new SchemaVersionTooNewException(9, 2)));
        yield return ("MigrationBackup", Beschreibe(new MigrationBackupFailedException(1, 2, "voll")));
        yield return ("Unbekannt", Beschreibe(new InvalidOperationException("etwas Unerwartetes")));
        yield return ("LaeuftBereits", StartupFailureText.AlreadyRunning(@"C:\Logs"));

        yield return ("MigrationGescheitert", Beschreibe(new MigrationFailedException(
            1, 2,
            new BackupResult
            {
                FileName = "ausgaben.zip",
                Primary = BackupOutcome.Succeeded,
                External = BackupOutcome.NotConfigured,
            },
            new InvalidOperationException("no such table"))));

        foreach (var problem in new[]
                 {
                     StorageProblem.DatabaseLocked, StorageProblem.DatabaseCorrupt,
                     StorageProblem.PathNotFound, StorageProblem.AccessDenied,
                     StorageProblem.DiskFull, StorageProblem.ReadOnly,
                 })
        {
            yield return ($"Speicher/{problem}", Beschreibe(AusnahmeZu(problem)));
        }
    }

    private static Exception AusnahmeZu(StorageProblem problem) => problem switch
    {
        StorageProblem.DatabaseLocked => new Microsoft.Data.Sqlite.SqliteException("locked", 5, 5),
        StorageProblem.DatabaseCorrupt => new Microsoft.Data.Sqlite.SqliteException("corrupt", 11, 11),
        StorageProblem.PathNotFound => new DirectoryNotFoundException("weg"),
        StorageProblem.AccessDenied => new UnauthorizedAccessException("nein"),
        StorageProblem.DiskFull => new IOException("voll", unchecked((int)0x80070070)),
        StorageProblem.ReadOnly => new IOException("schreibgeschützt", unchecked((int)0x80070013)),
        _ => new InvalidOperationException("egal"),
    };

    private static Exception WirfEcht()
    {
        try
        {
            throw new InvalidOperationException("Etwas ging schief");
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
