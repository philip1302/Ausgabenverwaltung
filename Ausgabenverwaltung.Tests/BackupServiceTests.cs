using System.IO.Compression;
using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.Startup;
using Microsoft.Data.Sqlite;

namespace Ausgabenverwaltung.Tests;

// Die Sicherung arbeitet auf echten Dateien - deshalb durchweg ein
// Temp-Verzeichnis statt ":memory:".
public class BackupServiceTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("ausgabenverwaltung-backup-");

    private string DatabasePath => Path.Combine(_tempDir.FullName, "ausgaben.db");
    private string BackupFolder => Path.Combine(_tempDir.FullName, "Backups");
    private string SettingsPath => Path.Combine(_tempDir.FullName, "settings.json");

    public void Dispose()
    {
        // Microsoft.Data.Sqlite haelt Dateihandles ueber ein natives
        // Verbindungspooling auch nach Dispose() offen - ohne dieses Leeren
        // schlaegt das Loeschen des Temp-Verzeichnisses fehl.
        SqliteConnection.ClearAllPools();
        _tempDir.Delete(recursive: true);
    }

    // Legt eine Datenbank mit den Grunddaten an (Person "Ich" usw.).
    private void LegeDatenbankAn() => StartupService.Run(DatabasePath, BackupFolder, SettingsPath);

    private BackupService ErzeugeDienst(System.Data.IDbConnection connection)
        => new(connection, BackupFolder, new BackupSettingsStore(SettingsPath));

    private static IReadOnlyList<string> ZipDateien(string ordner)
        => Directory.Exists(ordner)
            ? Directory.GetFiles(ordner, "*.zip").Select(pfad => Path.GetFileName(pfad)).OrderBy(name => name).ToList()
            : [];

    // ---------------- Erzeugen ----------------

    [Fact]
    public void Sicherung_erzeugt_eine_entpackbare_ZIP_mit_lesbarer_Datenbank()
    {
        LegeDatenbankAn();

        using (var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DatabasePath}"))
        {
            new PersonRepository(connection).Create("Frau Weber");

            var ergebnis = ErzeugeDienst(connection).RunNow(new DateTime(2026, 7, 30, 18, 42, 0));

            Assert.Equal(BackupOutcome.Succeeded, ergebnis.Primary);
            Assert.Null(ergebnis.PrimaryError);
            Assert.Equal("ausgaben_2026-07-30_1842.zip", ergebnis.FileName);
        }

        var zipPfad = Path.Combine(BackupFolder, "ausgaben_2026-07-30_1842.zip");
        Assert.True(File.Exists(zipPfad));

        // Die Zwischendatei aus Schritt 1 darf nicht liegen bleiben.
        Assert.Empty(Directory.GetFiles(BackupFolder, "*.tmp"));

        var entpackt = Path.Combine(_tempDir.FullName, "entpackt");
        Directory.CreateDirectory(entpackt);

        using (var archiv = ZipFile.OpenRead(zipPfad))
        {
            var eintrag = Assert.Single(archiv.Entries);

            // Der Eintrag heisst wie die aktive Datenbank - Wiederherstellen
            // ist damit Entpacken und Ersetzen, ohne Umbenennen.
            Assert.Equal("ausgaben.db", eintrag.FullName);
            eintrag.ExtractToFile(Path.Combine(entpackt, eintrag.FullName));
        }

        using var kopie = SqliteConnectionFactory.OpenConnection(
            $"Data Source={Path.Combine(entpackt, "ausgaben.db")}");

        var personen = new PersonRepository(kopie).GetAll().Select(person => person.Name).ToList();
        Assert.Contains("Ich", personen);
        Assert.Contains("Frau Weber", personen);
    }

    [Fact]
    public void Die_laufende_Datenbankdatei_bleibt_nach_der_Sicherung_benutzbar()
    {
        LegeDatenbankAn();

        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DatabasePath}");
        var repository = new PersonRepository(connection);

        ErzeugeDienst(connection).RunNow(new DateTime(2026, 7, 30, 18, 42, 0));

        repository.Create("Nach der Sicherung");
        Assert.Contains(repository.GetAll(), person => person.Name == "Nach der Sicherung");
    }

    // ---------------- Tagesbegrenzung ----------------

    [Fact]
    public void Zweiter_Start_am_selben_Tag_erzeugt_keine_zweite_Sicherung()
    {
        // Erster Lauf ist der Erststart: dort gibt es nichts zu sichern.
        LegeDatenbankAn();
        Assert.Empty(ZipDateien(BackupFolder));

        var zweiterStart = StartupService.Run(DatabasePath, BackupFolder, SettingsPath);
        var dritterStart = StartupService.Run(DatabasePath, BackupFolder, SettingsPath);

        Assert.Equal(BackupOutcome.Succeeded, zweiterStart.Backup!.Primary);
        Assert.Equal(BackupOutcome.Skipped, dritterStart.Backup!.Primary);
        Assert.Single(ZipDateien(BackupFolder));
    }

    [Fact]
    public void Sicherung_jetzt_uebergeht_die_Tagesbegrenzung()
    {
        LegeDatenbankAn();

        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DatabasePath}");
        var dienst = ErzeugeDienst(connection);

        dienst.RunIfDue(new DateTime(2026, 7, 30, 9, 0, 0));

        var uebersprungen = dienst.RunIfDue(new DateTime(2026, 7, 30, 11, 0, 0));
        var vonHand = dienst.RunNow(new DateTime(2026, 7, 30, 11, 0, 0));

        Assert.Equal(BackupOutcome.Skipped, uebersprungen.Primary);
        Assert.Equal(BackupOutcome.Succeeded, vonHand.Primary);
        Assert.Equal(
            new[] { "ausgaben_2026-07-30_0900.zip", "ausgaben_2026-07-30_1100.zip" },
            ZipDateien(BackupFolder));
    }

    [Fact]
    public void Am_naechsten_Tag_wird_wieder_gesichert()
    {
        LegeDatenbankAn();

        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DatabasePath}");
        var dienst = ErzeugeDienst(connection);

        dienst.RunIfDue(new DateTime(2026, 7, 30, 9, 0, 0));
        var naechsterTag = dienst.RunIfDue(new DateTime(2026, 7, 31, 9, 0, 0));

        Assert.Equal(BackupOutcome.Succeeded, naechsterTag.Primary);
        Assert.Equal(2, ZipDateien(BackupFolder).Count);
    }

    // ---------------- Aufbewahrung ----------------

    [Fact]
    public void Der_Lauf_raeumt_nach_der_Staffelung_auf_und_laesst_fremde_Dateien_liegen()
    {
        LegeDatenbankAn();
        Directory.CreateDirectory(BackupFolder);

        // 15 aeltere Sicherungen im Juni plus eine fremde Datei.
        for (var tag = 1; tag <= 15; tag++)
        {
            var name = BackupFileName.Create(new DateTime(2026, 6, tag, 9, 0, 0));
            File.WriteAllText(Path.Combine(BackupFolder, name), "Platzhalter");
        }

        var fremd = Path.Combine(BackupFolder, "liesmich.txt");
        File.WriteAllText(fremd, "Nicht anfassen.");

        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DatabasePath}");
        ErzeugeDienst(connection).RunNow(new DateTime(2026, 7, 30, 18, 42, 0));

        var verbleibend = ZipDateien(BackupFolder);

        // Die neun juengsten Juni-Staende plus die neue Sicherung ergeben
        // die zehn juengsten; zusaetzlich bleibt der letzte Juni-Stand -
        // der ist hier derselbe wie der juengste, also bleiben zehn.
        Assert.Equal(10, verbleibend.Count);
        Assert.Contains("ausgaben_2026-07-30_1842.zip", verbleibend);
        Assert.Contains("ausgaben_2026-06-15_0900.zip", verbleibend);
        Assert.DoesNotContain("ausgaben_2026-06-06_0900.zip", verbleibend);
        Assert.True(File.Exists(fremd));
    }

    // ---------------- Zweites Ziel ----------------

    [Fact]
    public void Erreichbares_zweites_Ziel_bekommt_eine_Kopie_und_setzt_den_Zeitstempel()
    {
        LegeDatenbankAn();

        var externerOrdner = Path.Combine(_tempDir.FullName, "Stick");
        var einstellungen = new BackupSettingsStore(SettingsPath);
        einstellungen.Save(new BackupSettings { ExternalFolderPath = externerOrdner });

        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DatabasePath}");
        var ergebnis = ErzeugeDienst(connection).RunNow(new DateTime(2026, 7, 30, 18, 42, 0));

        Assert.Equal(BackupOutcome.Succeeded, ergebnis.External);
        Assert.Equal(new[] { "ausgaben_2026-07-30_1842.zip" }, ZipDateien(externerOrdner));
        Assert.NotNull(einstellungen.Load().LastExternalBackupUtc);
    }

    [Fact]
    public void Nicht_erreichbares_zweites_Ziel_bricht_den_Lauf_nicht_ab()
    {
        LegeDatenbankAn();

        // Ein Pfad, der nicht angelegt werden KANN: sein Elternteil ist
        // eine Datei. Das steht stellvertretend fuer abgezogenen Stick,
        // getrenntes Netzlaufwerk oder fehlende Berechtigung.
        var blockierer = Path.Combine(_tempDir.FullName, "keinOrdner.txt");
        File.WriteAllText(blockierer, "Ich bin eine Datei.");

        var einstellungen = new BackupSettingsStore(SettingsPath);
        einstellungen.Save(new BackupSettings
        {
            ExternalFolderPath = Path.Combine(blockierer, "Sicherungen"),
        });

        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DatabasePath}");
        var ergebnis = ErzeugeDienst(connection).RunNow(new DateTime(2026, 7, 30, 18, 42, 0));

        // Ziel 1 lief - genau deshalb gibt es beim Start keinen Dialog.
        Assert.Equal(BackupOutcome.Succeeded, ergebnis.Primary);
        Assert.False(ergebnis.NeedsAttention);
        Assert.Single(ZipDateien(BackupFolder));

        Assert.Equal(BackupOutcome.Failed, ergebnis.External);
        Assert.NotNull(ergebnis.ExternalError);

        // Der Zeitstempel bleibt stehen - er ist die einzige Spur, an der
        // die Einstellungen spaeter "vor 12 Tagen" ablesen.
        Assert.Null(einstellungen.Load().LastExternalBackupUtc);
    }

    [Fact]
    public void Programmstart_mit_nicht_erreichbarem_zweitem_Ziel_laeuft_durch()
    {
        LegeDatenbankAn();

        var blockierer = Path.Combine(_tempDir.FullName, "keinOrdner.txt");
        File.WriteAllText(blockierer, "Ich bin eine Datei.");

        new BackupSettingsStore(SettingsPath).Save(new BackupSettings
        {
            ExternalFolderPath = Path.Combine(blockierer, "Sicherungen"),
        });

        var start = StartupService.Run(DatabasePath, BackupFolder, SettingsPath);

        Assert.Equal(BackupOutcome.Succeeded, start.Backup!.Primary);
        Assert.Equal(BackupOutcome.Failed, start.Backup!.External);
        Assert.False(start.IsFirstStart);
    }

    [Fact]
    public void Ohne_zweites_Ziel_bleibt_es_bei_Ziel_eins()
    {
        LegeDatenbankAn();

        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DatabasePath}");
        var ergebnis = ErzeugeDienst(connection).RunNow(new DateTime(2026, 7, 30, 18, 42, 0));

        Assert.Equal(BackupOutcome.Succeeded, ergebnis.Primary);
        Assert.Equal(BackupOutcome.NotConfigured, ergebnis.External);
        Assert.Null(ergebnis.ExternalError);
    }

    // ---------------- Auflistung ----------------

    [Fact]
    public void ListBackups_liefert_die_neueste_Sicherung_zuerst()
    {
        LegeDatenbankAn();

        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DatabasePath}");
        var dienst = ErzeugeDienst(connection);

        dienst.RunNow(new DateTime(2026, 7, 29, 9, 0, 0));
        dienst.RunNow(new DateTime(2026, 7, 30, 9, 0, 0));

        var sicherungen = dienst.ListBackups();

        Assert.Equal(2, sicherungen.Count);
        Assert.Equal("ausgaben_2026-07-30_0900.zip", sicherungen[0].FileName);
        Assert.True(sicherungen[0].SizeBytes > 0);
    }
}
