using Ausgabenverwaltung.Core.Backups;

namespace Ausgabenverwaltung.Tests;

public class BackupSettingsStoreTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("ausgabenverwaltung-settings-");

    private string SettingsPath => Path.Combine(_tempDir.FullName, "settings.json");

    public void Dispose() => _tempDir.Delete(recursive: true);

    [Fact]
    public void Ohne_Datei_gelten_die_Vorgabewerte()
    {
        var einstellungen = new BackupSettingsStore(SettingsPath).Load();

        Assert.Null(einstellungen.ExternalFolderPath);
        Assert.Null(einstellungen.LastExternalBackupUtc);
    }

    [Fact]
    public void Gespeicherte_Werte_kommen_unveraendert_zurueck()
    {
        var speicher = new BackupSettingsStore(SettingsPath);
        var zuletzt = new DateTime(2026, 7, 18, 6, 5, 4, DateTimeKind.Utc);

        speicher.Save(new BackupSettings
        {
            ExternalFolderPath = @"D:\Sicherungen",
            LastExternalBackupUtc = zuletzt,
        });

        var gelesen = speicher.Load();

        Assert.Equal(@"D:\Sicherungen", gelesen.ExternalFolderPath);
        Assert.Equal(zuletzt, gelesen.LastExternalBackupUtc);
    }

    [Fact]
    public void Der_Zeitstempel_steht_im_vorgeschriebenen_Format_in_der_Datei()
    {
        new BackupSettingsStore(SettingsPath).Save(new BackupSettings
        {
            LastExternalBackupUtc = new DateTime(2026, 7, 18, 6, 5, 4, DateTimeKind.Utc),
        });

        Assert.Contains("2026-07-18T06:05:04Z", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Eine_beschaedigte_Datei_verhindert_den_Start_nicht()
    {
        File.WriteAllText(SettingsPath, "{ das ist kein JSON");

        var einstellungen = new BackupSettingsStore(SettingsPath).Load();

        Assert.Null(einstellungen.ExternalFolderPath);
    }

    [Fact]
    public void Ein_unlesbarer_Zeitstempel_kostet_nicht_den_Pfad()
    {
        File.WriteAllText(
            SettingsPath,
            """{ "ExternalFolderPath": "D:\\Sicherungen", "LastExternalBackupUtc": "gestern" }""");

        var einstellungen = new BackupSettingsStore(SettingsPath).Load();

        Assert.Equal(@"D:\Sicherungen", einstellungen.ExternalFolderPath);
        Assert.Null(einstellungen.LastExternalBackupUtc);
    }
}
