using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Startup;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die eine Zusage, die sich nicht durch Nachdenken belegen laesst: eine
/// abgebrochene Schema-Migration laesst die Datenbank UNVERAENDERT
/// zurueck. Sie muss an einem Schritt vorgefuehrt werden, der tatsaechlich
/// scheitert - deshalb die Ueberladung von
/// <see cref="DatabaseMigrator.MigrateToLatest(System.Data.IDbConnection,
/// IReadOnlyList{ValueTuple{int, string}}, Func{string, string})"/>.
/// </summary>
public class MigrationRollbackTests : IDisposable
{
    private readonly DirectoryInfo _tempDir =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-migration-");

    private string DbPfad => Path.Combine(_tempDir.FullName, "ausgaben.db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _tempDir.Delete(recursive: true);
    }

    [Fact]
    public void Ein_gescheiterter_Schritt_nimmt_seine_Aenderungen_zurueck()
    {
        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DbPfad}");
        LegeVersion1An(connection);

        // Ein Schritt, der erst etwas veraendert und dann scheitert. Genau
        // dieser Zwischenstand darf nicht stehen bleiben.
        const string kaputterSchritt = """
            ALTER TABLE Kunde ADD COLUMN Farbe TEXT;
            UPDATE Kunde SET Farbe = 'rot';
            INSERT INTO SchemaVersion (Version, AppliedUtc) VALUES (2, '2026-01-01T00:00:00Z');
            INSERT INTO GibtEsNicht (Spalte) VALUES (1);
            """;

        Assert.ThrowsAny<SqliteException>(() => DatabaseMigrator.MigrateToLatest(
            connection, [(1, "kaputt.sql")], _ => kaputterSchritt));

        // Die Spalte darf es nicht geben - SQLite nimmt auch DDL zurueck.
        Assert.DoesNotContain(
            "Farbe",
            connection.Query<string>("SELECT name FROM pragma_table_info('Kunde')"));

        // Und der Stand steht unveraendert auf 1.
        Assert.Equal(1, DatabaseInitializer.GetSchemaVersion(connection));

        // Die Daten sind vollstaendig da.
        Assert.Equal(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Kunde"));
    }

    [Fact]
    public void Ein_gelungener_Schritt_bleibt_stehen()
    {
        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DbPfad}");
        LegeVersion1An(connection);

        const string guterSchritt = """
            ALTER TABLE Kunde ADD COLUMN Farbe TEXT;
            INSERT INTO SchemaVersion (Version, AppliedUtc) VALUES (2, '2026-01-01T00:00:00Z');
            """;

        var erreicht = DatabaseMigrator.MigrateToLatest(
            connection, [(1, "gut.sql")], _ => guterSchritt);

        Assert.Equal(2, erreicht);
        Assert.Contains(
            "Farbe",
            connection.Query<string>("SELECT name FROM pragma_table_info('Kunde')"));
    }

    [Fact]
    public void Ein_erster_gelungener_Schritt_ueberlebt_den_Abbruch_des_zweiten()
    {
        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DbPfad}");
        LegeVersion1An(connection);

        const string schrittEins = """
            ALTER TABLE Kunde ADD COLUMN Farbe TEXT;
            INSERT INTO SchemaVersion (Version, AppliedUtc) VALUES (2, '2026-01-01T00:00:00Z');
            """;

        const string schrittZwei = """
            ALTER TABLE Kunde ADD COLUMN Form TEXT;
            INSERT INTO GibtEsNicht (Spalte) VALUES (1);
            """;

        Assert.ThrowsAny<SqliteException>(() => DatabaseMigrator.MigrateToLatest(
            connection,
            [(1, "eins.sql"), (2, "zwei.sql")],
            name => name == "eins.sql" ? schrittEins : schrittZwei));

        var spalten = connection.Query<string>("SELECT name FROM pragma_table_info('Kunde')").ToList();

        // Schritt 1 ist fuer sich vollstaendig und in SchemaVersion
        // vermerkt - er bleibt.
        Assert.Contains("Farbe", spalten);
        Assert.Equal(2, DatabaseInitializer.GetSchemaVersion(connection));

        // Schritt 2 ist vollstaendig zurueckgenommen.
        Assert.DoesNotContain("Form", spalten);
    }

    // ================= Zusammenspiel mit dem Programmstart =================

    [Fact]
    public void Die_Meldung_zur_gescheiterten_Migration_verweist_auf_die_Sicherung()
    {
        var sicherung = new BackupResult
        {
            FileName = "ausgaben_2026-07-31_1842.zip",
            Primary = BackupOutcome.Succeeded,
            External = BackupOutcome.NotConfigured,
        };

        var ausnahme = new MigrationFailedException(
            1, 2, sicherung, new InvalidOperationException("no such table: GibtEsNicht"));

        var fehler = StartupFailureText.Describe(
            ausnahme, DbPfad, @"C:\Backups", @"C:\Logs", "1.0.0-test");

        // Der wichtigste Satz: die Daten sind unveraendert.
        Assert.Contains("unverändert", fehler.Message);
        Assert.Contains("zurückgenommen", fehler.Message);

        // Und wo der Rueckweg liegt.
        Assert.Contains("ausgaben_2026-07-31_1842.zip", fehler.Message);
        Assert.Equal(@"C:\Backups", fehler.FolderPath);

        // Der technische Grund gehoert in den aufklappbaren Bereich.
        Assert.Contains("GibtEsNicht", fehler.Technical);
        Assert.DoesNotContain("GibtEsNicht", fehler.Message);
    }

    [Fact]
    public void Eine_gescheiterte_Sicherung_verhindert_die_Migration_ganz()
    {
        // Der Sicherungsordner kann nicht entstehen: Elternteil ist eine
        // Datei. Ohne Sicherung wird nicht migriert (Regel: eine
        // Umstellung ohne Netz gibt es nicht).
        var blockierer = Path.Combine(_tempDir.FullName, "keinOrdner.txt");
        File.WriteAllText(blockierer, "Ich bin eine Datei.");

        using (var alt = SqliteConnectionFactory.OpenConnection($"Data Source={DbPfad}"))
        {
            alt.Execute(DatabaseInitializer.LoadScript("schema_v1.sql"));
        }

        SqliteConnection.ClearAllPools();

        Assert.Throws<MigrationBackupFailedException>(() => StartupService.Run(
            DbPfad,
            Path.Combine(blockierer, "Backups"),
            Path.Combine(_tempDir.FullName, "settings.json")));

        // Der Stand ist unveraendert geblieben.
        using var connection = SqliteConnectionFactory.OpenConnection($"Data Source={DbPfad}");
        Assert.Equal(1, DatabaseInitializer.GetSchemaVersion(connection));
    }

    // Eine kleine Datenbank im Stand 1, unabhaengig vom echten Schema:
    // hier geht es um das Verhalten des Migrators, nicht um die
    // tatsaechlichen Tabellen der Anwendung.
    private static void LegeVersion1An(System.Data.IDbConnection connection)
    {
        connection.Execute("""
            CREATE TABLE SchemaVersion (Version INTEGER NOT NULL, AppliedUtc TEXT NOT NULL);
            INSERT INTO SchemaVersion (Version, AppliedUtc) VALUES (1, '2026-01-01T00:00:00Z');

            CREATE TABLE Kunde (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);
            INSERT INTO Kunde (Id, Name) VALUES (1, 'Testeintrag');
            """);
    }
}
