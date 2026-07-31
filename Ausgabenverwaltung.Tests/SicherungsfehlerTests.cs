using System.Data;
using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Settings;
using Ausgabenverwaltung.Core.Startup;
using Ausgabenverwaltung.ViewModels;
using Microsoft.Data.Sqlite;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Nicht schreibbare Sicherungsziele.
///
/// Der Grundsatz dahinter: eine gescheiterte Sicherung haelt den Start
/// nicht auf - sie darf aber auch nicht unbemerkt bleiben. Sichtbar wird
/// sie im Einstellungsbereich, und zwar dauerhaft, nicht nur als Band, das
/// jemand wegklickt.
/// </summary>
public class SicherungsfehlerTests : IDisposable
{
    private readonly DirectoryInfo _tempDir =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-sicherungsfehler-");

    private string DbPfad => Path.Combine(_tempDir.FullName, "ausgaben.db");
    private string EinstellungenPfad => Path.Combine(_tempDir.FullName, "settings.json");

    // Ein Ordnerpfad, der nicht entstehen kann: sein Elternteil ist eine
    // Datei.
    private string UnbeschreibbarerOrdner
    {
        get
        {
            var blockierer = Path.Combine(_tempDir.FullName, "keinOrdner.txt");
            if (!File.Exists(blockierer))
            {
                File.WriteAllText(blockierer, "Ich bin eine Datei.");
            }

            return Path.Combine(blockierer, "Sicherungen");
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _tempDir.Delete(recursive: true);
    }

    [Fact]
    public void Ein_nicht_schreibbares_Ziel_1_meldet_den_Fehlschlag_ohne_zu_werfen()
    {
        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DbPfad}");
        DatabaseInitializer.Initialize(connection);

        var dienst = new BackupService(
            connection, UnbeschreibbarerOrdner, new AppSettingsStore(EinstellungenPfad));

        // Der Dienst wirft nie - jeder Fehler landet im Ergebnis.
        var ergebnis = dienst.RunNow(DateTime.Now);

        Assert.Equal(BackupOutcome.Failed, ergebnis.Primary);
        Assert.True(ergebnis.NeedsAttention);
        Assert.False(ergebnis.CreatedBackup);
        Assert.NotNull(ergebnis.PrimaryError);
    }

    [Fact]
    public void Ein_nicht_schreibbares_Ziel_1_haelt_den_Start_nicht_auf()
    {
        // Die Anwendung startet - nur eben ohne Sicherung. Das ist der
        // ausdrueckliche Entwurf: lieber arbeiten koennen als gar nicht
        // starten.
        var ergebnis = StartupService.Run(DbPfad, UnbeschreibbarerOrdner, EinstellungenPfad);

        Assert.True(ergebnis.IsFirstStart);

        // Beim allerersten Start gibt es nichts zu sichern - deshalb ein
        // zweiter Lauf, bei dem tatsaechlich gesichert wuerde.
        SqliteConnection.ClearAllPools();
        var zweiterStart = StartupService.Run(DbPfad, UnbeschreibbarerOrdner, EinstellungenPfad);

        Assert.False(zweiterStart.IsFirstStart);
        Assert.NotNull(zweiterStart.Backup);
        Assert.True(zweiterStart.Backup!.NeedsAttention);
    }

    [Fact]
    public void Der_Fehlschlag_bleibt_im_Einstellungsbereich_dauerhaft_sichtbar()
    {
        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DbPfad}");
        DatabaseInitializer.Initialize(connection);

        var einstellungen = new AppSettingsStore(EinstellungenPfad);
        var dienst = new BackupService(connection, UnbeschreibbarerOrdner, einstellungen);

        var startergebnis = new StartupResult
        {
            DatabaseFilePath = DbPfad,
            IsFirstStart = false,
            GeneratedExpenses = [],
            Backup = dienst.RunNow(DateTime.Now),
        };

        var vm = new DatensicherungViewModel(dienst, einstellungen, startergebnis);

        Assert.True(vm.Ziel1FehlerSichtbar);
        Assert.Contains("Datensicherung konnte nicht angelegt werden", vm.Ziel1FehlerText);

        // Und was das fuer die Daten bedeutet.
        Assert.Contains("nicht betroffen und unverändert", vm.Ziel1FehlerText);
    }

    [Fact]
    public void Eine_gelungene_Sicherung_nimmt_den_Hinweis_wieder_weg()
    {
        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DbPfad}");
        DatabaseInitializer.Initialize(connection);

        var einstellungen = new AppSettingsStore(EinstellungenPfad);

        // Erst ein Dienst, der scheitert - dann einer, der funktioniert.
        var kaputt = new BackupService(connection, UnbeschreibbarerOrdner, einstellungen);
        var gut = new BackupService(
            connection, Path.Combine(_tempDir.FullName, "Backups"), einstellungen);

        var vm = new DatensicherungViewModel(gut, einstellungen, new StartupResult
        {
            DatabaseFilePath = DbPfad,
            IsFirstStart = false,
            GeneratedExpenses = [],
            Backup = kaputt.RunNow(DateTime.Now),
        });

        Assert.True(vm.Ziel1FehlerSichtbar);

        vm.SicherungJetztCommand.Execute(null);

        Assert.False(vm.Ziel1FehlerSichtbar);
        Assert.Contains("Sicherung erstellt", vm.MeldungText);
    }

    [Fact]
    public void Ein_nicht_erreichbares_Ziel_2_bekommt_einen_eigenen_ruhigeren_Text()
    {
        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DbPfad}");
        DatabaseInitializer.Initialize(connection);

        var einstellungen = new AppSettingsStore(EinstellungenPfad);
        einstellungen.Save(einstellungen.Load() with
        {
            ExternalFolderPath = UnbeschreibbarerOrdner,
        });

        var dienst = new BackupService(
            connection, Path.Combine(_tempDir.FullName, "Backups"), einstellungen);

        var ergebnis = dienst.RunNow(DateTime.Now);

        // Ziel 1 hat funktioniert - nur die zweite Kopie fehlt.
        Assert.Equal(BackupOutcome.Succeeded, ergebnis.Primary);
        Assert.Equal(BackupOutcome.Failed, ergebnis.External);
        Assert.False(ergebnis.NeedsAttention);

        var text = FileErrorText.ForExternalBackup(ergebnis.ExternalProblem, "D:\\Stick");

        Assert.Contains("trotzdem angelegt worden", text);
        Assert.Contains("nur die zweite Kopie", text);
    }

    [Fact]
    public void Ein_nicht_beschreibbares_Ziel_2_wird_gar_nicht_erst_uebernommen()
    {
        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DbPfad}");
        DatabaseInitializer.Initialize(connection);

        var einstellungen = new AppSettingsStore(EinstellungenPfad);
        var dienst = new BackupService(
            connection, Path.Combine(_tempDir.FullName, "Backups"), einstellungen);

        var vm = new DatensicherungViewModel(dienst, einstellungen, new StartupResult
        {
            DatabaseFilePath = DbPfad,
            IsFirstStart = false,
            GeneratedExpenses = [],
        });

        vm.SetzeZweitesZiel(UnbeschreibbarerOrdner);

        Assert.True(vm.MeldungIstFehler);
        Assert.Contains("nicht als zweites Ziel übernommen", vm.MeldungText);

        // Nichts gespeichert: das Ziel bleibt uneingerichtet.
        Assert.False(vm.Ziel2Eingerichtet);
        Assert.Null(einstellungen.Load().ExternalFolderPath);
    }

    // ================= Einordnung der Ursachen =================

    [Theory]
    [InlineData(112, StorageProblem.DiskFull)]          // ERROR_DISK_FULL
    [InlineData(39, StorageProblem.DiskFull)]           // ERROR_HANDLE_DISK_FULL
    [InlineData(32, StorageProblem.FileInUse)]          // ERROR_SHARING_VIOLATION
    [InlineData(33, StorageProblem.FileInUse)]          // ERROR_LOCK_VIOLATION
    [InlineData(19, StorageProblem.ReadOnly)]           // ERROR_WRITE_PROTECT
    [InlineData(21, StorageProblem.PathNotFound)]       // ERROR_NOT_READY
    public void Windows_Fehlernummern_werden_richtig_eingeordnet(
        int fehlernummer, StorageProblem erwartet)
    {
        var ausnahme = new IOException("egal", unchecked((int)(0x80070000 | (uint)fehlernummer)));

        Assert.Equal(erwartet, StorageProblems.Classify(ausnahme));
    }

    [Theory]
    [InlineData(5, StorageProblem.DatabaseLocked)]      // SQLITE_BUSY
    [InlineData(6, StorageProblem.DatabaseLocked)]      // SQLITE_LOCKED
    [InlineData(11, StorageProblem.DatabaseCorrupt)]    // SQLITE_CORRUPT
    [InlineData(26, StorageProblem.DatabaseCorrupt)]    // SQLITE_NOTADB
    [InlineData(13, StorageProblem.DiskFull)]           // SQLITE_FULL
    [InlineData(8, StorageProblem.ReadOnly)]            // SQLITE_READONLY
    public void SQLite_Ergebniscodes_werden_richtig_eingeordnet(
        int ergebniscode, StorageProblem erwartet)
    {
        var ausnahme = new SqliteException("egal", ergebniscode, ergebniscode);

        Assert.Equal(erwartet, StorageProblems.Classify(ausnahme));
    }

    [Fact]
    public void Eine_fehlende_Berechtigung_wird_erkannt()
    {
        Assert.Equal(
            StorageProblem.AccessDenied,
            StorageProblems.Classify(new UnauthorizedAccessException()));
    }

    [Fact]
    public void Der_eigentliche_Grund_wird_auch_in_einer_inneren_Ausnahme_gefunden()
    {
        var innen = new IOException("voll", unchecked((int)0x80070070));
        var aussen = new AggregateException("Sammelmappe", innen);

        Assert.Equal(StorageProblem.DiskFull, StorageProblems.Classify(aussen));
    }

    // ================= CSV-Export =================

    [Fact]
    public void Die_von_Excel_gesperrte_Zieldatei_bekommt_einen_eigenen_Hinweis()
    {
        // Der mit Abstand haeufigste Fall beim Export.
        var text = FileErrorText.ForCsvExport(StorageProblem.FileInUse, "Auswertung.csv");

        Assert.Contains("Auswertung.csv", text);
        Assert.Contains("Excel", text);
        Assert.Contains("schließen", text);

        // Und die Auswertung selbst ist unberuehrt.
        Assert.Contains("unverändert", text);
    }
}
