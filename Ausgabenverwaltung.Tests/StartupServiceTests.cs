using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.People;
using Ausgabenverwaltung.Core.RecurringExpenses;
using Ausgabenverwaltung.Core.Startup;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Ausgabenverwaltung.Tests;

// StartupService oeffnet fuer jeden Run() eine eigene Verbindung und
// schliesst sie wieder - deshalb braucht dieser Test eine echte,
// dateibasierte SQLite-DB statt ":memory:", sonst waere jeder Run() eine
// frische, leere Datenbank und "zweiter Start dupliziert nichts" liesse
// sich gar nicht pruefen.
public class StartupServiceTests : IDisposable
{
    private readonly DirectoryInfo _tempDir = Directory.CreateTempSubdirectory("ausgabenverwaltung-tests-");

    // Sicherungsordner und Einstellungsdatei bewusst mit ins Temp-
    // Verzeichnis: die parameterlose Ueberladung von Run() wuerde in das
    // echte %APPDATA% des Ausfuehrenden sichern.
    private string BackupFolder => Path.Combine(_tempDir.FullName, "Backups");
    private string SettingsPath => Path.Combine(_tempDir.FullName, "settings.json");

    private StartupResult Starte(string databaseFilePath)
        => StartupService.Run(databaseFilePath, BackupFolder, SettingsPath);

    public void Dispose()
    {
        // Microsoft.Data.Sqlite haelt Dateihandles ueber ein natives
        // Verbindungspooling auch nach Dispose() der einzelnen
        // SqliteConnection offen - ohne dieses Leeren schlaegt das
        // Loeschen des Temp-Verzeichnisses mit IOException fehl.
        SqliteConnection.ClearAllPools();
        _tempDir.Delete(recursive: true);
    }

    [Fact]
    public void Erster_Start_legt_Datenbank_Ich_Person_und_Sonstiges_Kategorie_an()
    {
        var dbPath = Path.Combine(_tempDir.FullName, "ausgaben.db");

        var result = Starte(dbPath);

        Assert.True(File.Exists(dbPath));
        Assert.True(result.IsFirstStart);
        Assert.Equal(dbPath, result.DatabaseFilePath);
        Assert.Empty(result.GeneratedExpenses);
        Assert.Equal(0, result.GeneratedExpenseCount);

        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={dbPath}");

        var personen = new PersonRepository(connection).GetAll();
        Assert.Single(personen);
        Assert.Equal("Ich", personen[0].Name);
        Assert.True(personen[0].IsSelf);

        var kategorien = new CategoryRepository(connection).GetTree();
        Assert.Single(kategorien);
        Assert.Equal("Sonstiges", kategorien[0].Category.Name);
    }

    [Fact]
    public void Zweiter_Start_legt_Person_und_Kategorie_nicht_erneut_an()
    {
        var dbPath = Path.Combine(_tempDir.FullName, "ausgaben.db");

        var ersterStart = Starte(dbPath);
        var zweiterStart = Starte(dbPath);

        Assert.True(ersterStart.IsFirstStart);
        Assert.False(zweiterStart.IsFirstStart);

        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={dbPath}");

        var personen = new PersonRepository(connection).GetAll();
        Assert.Single(personen);

        var kategorien = new CategoryRepository(connection).GetTree();
        Assert.Single(kategorien);
    }

    [Fact]
    public void Neuere_SchemaVersion_als_erwartet_bricht_sauber_ab()
    {
        var dbPath = Path.Combine(_tempDir.FullName, "ausgaben.db");

        // Datenbank einmal regulaer anlegen, dann die SchemaVersion
        // manuell auf einen Stand setzen, den diese Programmversion
        // (noch) nicht kennt.
        Starte(dbPath);
        using (var connection = SqliteConnectionFactory.OpenConnection($"Data Source={dbPath}"))
        {
            connection.Execute("UPDATE SchemaVersion SET Version = 9999");
        }

        var exception = Assert.Throws<SchemaVersionTooNewException>(() => Starte(dbPath));

        Assert.Equal(9999, exception.ActualVersion);
        Assert.Equal(DatabaseInitializer.ExpectedSchemaVersion, exception.ExpectedVersion);
    }

    [Fact]
    public void Eine_Datenbank_der_Version_1_wird_beim_Start_hochgezogen()
    {
        var dbPath = Path.Combine(_tempDir.FullName, "ausgaben.db");

        // Eine Datenbank im alten Stand, wie sie die Vorgaengerversion
        // hinterlassen hat - mit Daten darin.
        using (var alt = SqliteConnectionFactory.OpenConnection($"Data Source={dbPath}"))
        {
            alt.Execute(DatabaseInitializer.LoadScript("schema_v1.sql"));
            alt.Execute("""
                INSERT INTO Person (Id, Name, IsSelf, IsArchived, CreatedUtc)
                VALUES (1, 'Ich', 1, 0, '2026-01-01T00:00:00Z');

                INSERT INTO Category (Id, ParentId, Name, SortOrder, IsArchived, CreatedUtc)
                VALUES (1, NULL, 'Pferde', 0, 0, '2026-01-01T00:00:00Z');

                INSERT INTO Expense
                    (Id, CategoryId, AmountCents, ExpenseDate, PayerId, CreatedUtc, ModifiedUtc)
                VALUES (1, 1, 4711, '2026-03-05', 1,
                        '2026-03-05T00:00:00Z', '2026-03-05T00:00:00Z');
                """);
        }

        var result = Starte(dbPath);

        Assert.NotNull(result.Migration);
        Assert.Equal(1, result.Migration!.FromVersion);
        Assert.Equal(DatabaseInitializer.ExpectedSchemaVersion, result.Migration.ToVersion);
        Assert.False(result.IsFirstStart);

        // Vor der Umstellung muss eine Sicherung entstanden sein - sie ist
        // der einzige Rueckweg.
        Assert.Equal(BackupOutcome.Succeeded, result.Migration.Backup.Primary);
        Assert.NotEmpty(Directory.GetFiles(BackupFolder, "*.zip"));

        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={dbPath}");
        Assert.Equal(DatabaseInitializer.ExpectedSchemaVersion, DatabaseInitializer.GetSchemaVersion(connection));
        Assert.Equal(4711L, connection.ExecuteScalar<long>(
            "SELECT AmountCents FROM Expense WHERE Id = 1"));
        Assert.Equal(
            CategoryColorPalette.DefaultHex,
            new CategoryRepository(connection).GetResolvedColors()[1]);
    }

    [Fact]
    public void Ein_zweiter_Start_migriert_nicht_noch_einmal()
    {
        var dbPath = Path.Combine(_tempDir.FullName, "ausgaben.db");

        Assert.Null(Starte(dbPath).Migration);
        Assert.Null(Starte(dbPath).Migration);
    }

    [Fact]
    public void Faellige_wiederkehrende_Buchung_wird_beim_naechsten_Start_erzeugt()
    {
        var dbPath = Path.Combine(_tempDir.FullName, "ausgaben.db");

        Starte(dbPath); // erster Start: legt Ich + Sonstiges an

        using (var connection = SqliteConnectionFactory.OpenConnection($"Data Source={dbPath}"))
        {
            var selfId = new PersonRepository(connection).GetAll().Single(p => p.IsSelf).Id;
            var categoryId = new CategoryRepository(connection).GetTree().Single().Category.Id;

            new RecurringExpenseRepository(connection).Create(
                categoryId, selfId, 1000, "Testabo", "month", 1, 1,
                new DateOnly(2026, 1, 1), null);
        }

        var zweiterStart = Starte(dbPath);

        Assert.NotEmpty(zweiterStart.GeneratedExpenses);
        Assert.Equal(zweiterStart.GeneratedExpenses.Count, zweiterStart.GeneratedExpenseCount);
    }
}
