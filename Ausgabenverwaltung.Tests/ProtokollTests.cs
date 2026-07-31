using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Logging;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Protokollierung. Geprueft werden drei Zusagen: eine Datei je Tag,
/// die letzten 30 Tage, und - die wichtigste - kein Inhalt der
/// Haushaltsfuehrung im Protokoll, damit es sich weitergeben laesst.
/// </summary>
public class ProtokollTests : IDisposable
{
    private readonly DirectoryInfo _tempDir =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-protokoll-");

    public void Dispose()
    {
        // Die Protokollierung ist eine statische Ausfuehrung. Ohne dieses
        // Zuruecksetzen schriebe der naechste Test noch in das Temp-
        // Verzeichnis dieses hier.
        AppLog.Stop();
        _tempDir.Delete(recursive: true);
    }

    // ---------------- Dateiname ----------------

    [Fact]
    public void Dateiname_traegt_den_Tag_und_laesst_sich_zurueckleseng()
    {
        var name = LogFileName.Create(new DateOnly(2026, 7, 31));

        Assert.Equal("ausgabenverwaltung_2026-07-31.log", name);
        Assert.True(LogFileName.TryParseDate(name, out var zurueck));
        Assert.Equal(new DateOnly(2026, 7, 31), zurueck);
    }

    [Theory]
    [InlineData("irgendwas.log")]
    [InlineData("ausgabenverwaltung_2026-13-99.log")]
    [InlineData("ausgabenverwaltung_2026-07-31.txt")]
    [InlineData("ausgaben_2026-07-31_1842.zip")]
    public void Fremde_Dateien_werden_nicht_als_Protokoll_erkannt(string dateiname)
    {
        Assert.False(LogFileName.TryParseDate(dateiname, out _));
    }

    // ---------------- Aufbewahrung ----------------

    [Fact]
    public void Die_letzten_dreissig_Tage_bleiben_alles_aeltere_faellt_weg()
    {
        var heute = new DateOnly(2026, 7, 31);

        var dateien = Enumerable.Range(0, 40)
            .Select(tage => Datei(heute.AddDays(-tage)))
            .ToList();

        var abgelaufen = LogRetention.SelectExpired(dateien, heute);

        // 30 Tage behalten heisst: heute und die 29 davor.
        Assert.Equal(10, abgelaufen.Count);
        Assert.All(abgelaufen, datei => Assert.True(datei.Date <= heute.AddDays(-30)));
        Assert.DoesNotContain(abgelaufen, datei => datei.Date == heute.AddDays(-29));
    }

    [Fact]
    public void Eine_Datei_mit_Datum_in_der_Zukunft_bleibt_liegen()
    {
        // Kommt bei zurueckgestellter Systemuhr vor. Ein Protokoll
        // wegzuwerfen, weil die Uhr falsch ging, waere die schlechtere der
        // beiden Moeglichkeiten.
        var heute = new DateOnly(2026, 7, 31);

        var abgelaufen = LogRetention.SelectExpired([Datei(heute.AddDays(5))], heute);

        Assert.Empty(abgelaufen);
    }

    // ---------------- Schreiben ----------------

    [Fact]
    public void Ein_Eintrag_landet_in_der_Datei_des_heutigen_Tages()
    {
        AppLog.Start(_tempDir.FullName, DateOnly.FromDateTime(DateTime.Now));
        AppLog.Current.Info("Ein Testeintrag.");

        var pfad = AppLog.Current.GetCurrentFilePath(DateOnly.FromDateTime(DateTime.Now));

        Assert.NotNull(pfad);
        Assert.Contains("Ein Testeintrag.", File.ReadAllText(pfad!));
    }

    [Fact]
    public void Eine_Ausnahme_kommt_mit_vollstaendigem_Aufrufstapel_ins_Protokoll()
    {
        AppLog.Start(_tempDir.FullName, DateOnly.FromDateTime(DateTime.Now));

        try
        {
            WirfTief();
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Testen", ex);
        }

        var inhalt = File.ReadAllText(
            AppLog.Current.GetCurrentFilePath(DateOnly.FromDateTime(DateTime.Now))!);

        Assert.Contains("Beim Testen", inhalt);
        Assert.Contains("InvalidOperationException", inhalt);

        // Der Aufrufstapel muss die Methode nennen, in der es passierte -
        // sonst ist der Eintrag zum Nachvollziehen wertlos.
        Assert.Contains(nameof(WirfTief), inhalt);

        // Und die innere Ausnahme darf nicht verloren gehen.
        Assert.Contains("Der eigentliche Grund", inhalt);
    }

    [Fact]
    public void Alte_Protokolle_werden_beim_Start_weggeraeumt()
    {
        var heute = new DateOnly(2026, 7, 31);

        // Eine alte und eine junge Datei von Hand hinlegen.
        var alt = Path.Combine(_tempDir.FullName, LogFileName.Create(heute.AddDays(-45)));
        var jung = Path.Combine(_tempDir.FullName, LogFileName.Create(heute.AddDays(-3)));
        var fremd = Path.Combine(_tempDir.FullName, "notizen.log");

        File.WriteAllText(alt, "alt");
        File.WriteAllText(jung, "jung");
        File.WriteAllText(fremd, "gehoert nicht uns");

        AppLog.Start(_tempDir.FullName, heute);

        Assert.False(File.Exists(alt));
        Assert.True(File.Exists(jung));

        // Fremde Dateien im Ordner werden nie angefasst.
        Assert.True(File.Exists(fremd));
    }

    // ---------------- Nie abstuerzen ----------------

    [Fact]
    public void Ohne_Start_wird_stillschweigend_nichts_geschrieben()
    {
        AppLog.Stop();

        // Kein Wurf, kein Ordner, keine Datei - einfach nichts.
        AppLog.Current.Info("Geht ins Leere.");
        AppLog.Current.Exception("Auch das", new InvalidOperationException("egal"));

        Assert.False(AppLog.Current.IsEnabled);
        Assert.Null(AppLog.Current.FolderPath);
        Assert.Empty(AppLog.Current.ListFiles());
    }

    [Fact]
    public void Ein_unbrauchbarer_Ordner_verhindert_den_Start_nicht()
    {
        // Elternteil ist eine Datei - der Ordner kann nicht entstehen.
        var blockierer = Path.Combine(_tempDir.FullName, "keinOrdner.txt");
        File.WriteAllText(blockierer, "Ich bin eine Datei.");

        AppLog.Start(Path.Combine(blockierer, "Logs"), DateOnly.FromDateTime(DateTime.Now));

        // Kein Wurf - und die Anwendung laeuft eben ohne Protokoll weiter.
        Assert.False(AppLog.Current.IsEnabled);
        AppLog.Current.Info("Auch das geht ins Leere.");
    }

    [Fact]
    public void Ein_verschwundener_Ordner_laesst_das_Schreiben_still_scheitern()
    {
        var unterordner = Path.Combine(_tempDir.FullName, "Logs");
        AppLog.Start(unterordner, DateOnly.FromDateTime(DateTime.Now));
        Assert.True(AppLog.Current.IsEnabled);

        // Jemand raeumt den Ordner weg, waehrend die Anwendung laeuft.
        Directory.Delete(unterordner, recursive: true);

        AppLog.Current.Info("Faellt unter den Tisch.");
        AppLog.Current.Exception("Auch das", new InvalidOperationException("egal"));
    }

    // ---------------- Teilbarkeit ----------------
    //
    // Der eigentliche Grund fuer LogEvents: das Protokoll soll sich
    // weitergeben lassen, ohne dass die Haushaltsfuehrung mitgeht.

    [Fact]
    public void Der_Erzeugungslauf_nennt_nur_die_Anzahl_keine_Inhalte()
    {
        var text = LogEvents.RecurringGenerated(3, new DateOnly(2026, 7, 31));

        Assert.Contains("3", text);
        Assert.Contains("2026-07-31", text);

        // Kein Betrag, kein Titel, keine Bemerkung.
        Assert.DoesNotContain("€", text);
        Assert.DoesNotContain(",", text.Replace("(Stichtag", string.Empty));
    }

    [Fact]
    public void Die_Sicherung_nennt_nur_Ergebnis_und_Dateinamen()
    {
        var text = LogEvents.Backup(new BackupResult
        {
            FileName = "ausgaben_2026-07-31_1842.zip",
            Primary = BackupOutcome.Succeeded,
            External = BackupOutcome.NotConfigured,
        });

        Assert.Contains("ausgaben_2026-07-31_1842.zip", text);
        Assert.Contains("nicht eingerichtet", text);

        // Der Dateiname ist ein reiner Zeitstempel und verraet nichts.
        Assert.DoesNotContain("€", text);
    }

    [Fact]
    public void Der_Programmstart_nennt_Version_und_Datenbankpfad()
    {
        // Beides ausdruecklich gewollt: ohne den Pfad laesst sich nicht
        // beantworten, welche Datei die Anwendung geoeffnet hat, und das
        // ist bei einem Fehlerbericht die erste Frage.
        var text = LogEvents.ProgramStart("1.4.2", @"C:\Users\Paul\AppData\Roaming\x\ausgaben.db");

        Assert.Contains("1.4.2", text);
        Assert.Contains("ausgaben.db", text);
    }

    private static LogFile Datei(DateOnly tag)
        => new(Path.Combine("egal", LogFileName.Create(tag)), LogFileName.Create(tag), tag);

    private static void WirfTief()
        => throw new InvalidOperationException(
            "Aussen", new InvalidOperationException("Der eigentliche Grund"));
}
