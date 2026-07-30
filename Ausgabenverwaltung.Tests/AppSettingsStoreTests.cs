using Ausgabenverwaltung.Core.Display;
using Ausgabenverwaltung.Core.Settings;

namespace Ausgabenverwaltung.Tests;

public class AppSettingsStoreTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("ausgabenverwaltung-settings-");

    private string SettingsPath => Path.Combine(_tempDir.FullName, "settings.json");

    public void Dispose() => _tempDir.Delete(recursive: true);

    [Fact]
    public void Ohne_Datei_gelten_die_Vorgabewerte()
    {
        var einstellungen = new AppSettingsStore(SettingsPath).Load();

        Assert.Null(einstellungen.ExternalFolderPath);
        Assert.Null(einstellungen.LastExternalBackupUtc);
        Assert.Equal(FontScales.DefaultFactor, einstellungen.FontScale);
        Assert.Equal(ColumnWidths.CategoryDefault, einstellungen.CategoryColumnWidth);
    }

    [Fact]
    public void Gespeicherte_Werte_kommen_unveraendert_zurueck()
    {
        var speicher = new AppSettingsStore(SettingsPath);
        var zuletzt = new DateTime(2026, 7, 18, 6, 5, 4, DateTimeKind.Utc);

        speicher.Save(new AppSettings
        {
            ExternalFolderPath = @"D:\Sicherungen",
            LastExternalBackupUtc = zuletzt,
            FontScale = FontScales.Factor(FontScaleStep.ExtraLarge),
            CategoryColumnWidth = 340,
        });

        var gelesen = speicher.Load();

        Assert.Equal(@"D:\Sicherungen", gelesen.ExternalFolderPath);
        Assert.Equal(zuletzt, gelesen.LastExternalBackupUtc);
        Assert.Equal(2.0, gelesen.FontScale);
        Assert.Equal(340, gelesen.CategoryColumnWidth);
    }

    // Dieselbe Vorsicht wie bei der Schriftgroesse: eine Datei aus der
    // Zeit vor der ziehbaren Spalte hat den Wert nicht, und "0" waere
    // hier eine unsichtbare Kategoriespalte.
    [Fact]
    public void Eine_Datei_ohne_Spaltenbreite_bleibt_bei_der_Vorgabe()
    {
        File.WriteAllText(SettingsPath, """{ "FontScale": 1.4 }""");

        Assert.Equal(
            ColumnWidths.CategoryDefault,
            new AppSettingsStore(SettingsPath).Load().CategoryColumnWidth);
    }

    [Fact]
    public void Eine_unsinnige_Spaltenbreite_wird_beim_Laden_zurechtgerueckt()
    {
        File.WriteAllText(SettingsPath, """{ "CategoryColumnWidth": 9000 }""");

        Assert.Equal(
            ColumnWidths.CategoryMax,
            new AppSettingsStore(SettingsPath).Load().CategoryColumnWidth);
    }

    [Fact]
    public void Der_Zeitstempel_steht_im_vorgeschriebenen_Format_in_der_Datei()
    {
        new AppSettingsStore(SettingsPath).Save(new AppSettings
        {
            LastExternalBackupUtc = new DateTime(2026, 7, 18, 6, 5, 4, DateTimeKind.Utc),
        });

        Assert.Contains("2026-07-18T06:05:04Z", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void Eine_beschaedigte_Datei_verhindert_den_Start_nicht()
    {
        File.WriteAllText(SettingsPath, "{ das ist kein JSON");

        var einstellungen = new AppSettingsStore(SettingsPath).Load();

        Assert.Null(einstellungen.ExternalFolderPath);
        Assert.Equal(FontScales.DefaultFactor, einstellungen.FontScale);
    }

    [Fact]
    public void Ein_unlesbarer_Zeitstempel_kostet_nicht_den_Pfad()
    {
        File.WriteAllText(
            SettingsPath,
            """{ "ExternalFolderPath": "D:\\Sicherungen", "LastExternalBackupUtc": "gestern" }""");

        var einstellungen = new AppSettingsStore(SettingsPath).Load();

        Assert.Equal(@"D:\Sicherungen", einstellungen.ExternalFolderPath);
        Assert.Null(einstellungen.LastExternalBackupUtc);
    }

    // Eine Datei aus der Zeit vor der Schriftgroessen-Einstellung: der
    // fehlende Wert darf nicht als "0" gelesen werden, sonst startet die
    // Anwendung nach dem Update in der kleinsten Stufe.
    [Fact]
    public void Eine_Datei_ohne_Schriftgroesse_bleibt_bei_Normal()
    {
        File.WriteAllText(SettingsPath, """{ "ExternalFolderPath": "D:\\Sicherungen" }""");

        var einstellungen = new AppSettingsStore(SettingsPath).Load();

        Assert.Equal(FontScales.DefaultFactor, einstellungen.FontScale);
    }

    [Fact]
    public void Ein_krummer_Skalierungsfaktor_wird_beim_Laden_auf_eine_Stufe_gerundet()
    {
        File.WriteAllText(SettingsPath, """{ "FontScale": 1.36 }""");

        Assert.Equal(1.4, new AppSettingsStore(SettingsPath).Load().FontScale);
    }

    [Fact]
    public void Die_Schriftgroesse_bleibt_beim_Speichern_des_Sicherungsziels_erhalten()
    {
        var speicher = new AppSettingsStore(SettingsPath);
        speicher.Save(new AppSettings { FontScale = 1.4, CategoryColumnWidth = 300 });

        // So aendert die Oberflaeche einzelne Werte: laden, mit "with"
        // weiterschreiben. Genau dafuer gibt es nur diesen einen Speicher.
        speicher.Save(speicher.Load() with { ExternalFolderPath = @"E:\Stick" });

        var gelesen = speicher.Load();
        Assert.Equal(@"E:\Stick", gelesen.ExternalFolderPath);
        Assert.Equal(1.4, gelesen.FontScale);
        Assert.Equal(300, gelesen.CategoryColumnWidth);
    }
}
