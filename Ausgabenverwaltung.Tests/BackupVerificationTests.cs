using System.IO.Compression;
using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Errors;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Pruefung einer Sicherungsdatei.
///
/// Der Grundsatz dahinter: eine Sicherung ist nur so viel wert wie ihre
/// Wiederherstellbarkeit. Eine Liste von Dateinamen belegt gar nichts -
/// erst wer die Datei einmal geoeffnet und durchgezaehlt hat, weiss es.
///
/// Geprueft wird deshalb nicht nur der gute Fall, sondern jeder Weg, auf
/// dem eine Sicherung unbrauchbar sein kann - und in allen Faellen, dass
/// hinterher kein temporaerer Ordner liegen bleibt.
/// </summary>
public class BackupVerificationTests : IDisposable
{
    private readonly DirectoryInfo _tempDir =
        Directory.CreateTempSubdirectory("ausgabenverwaltung-pruefung-tests-");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _tempDir.Delete(recursive: true);
    }

    // Wie viele Arbeitsordner der Pruefung gerade im Temp-Verzeichnis
    // liegen. Die Pruefung raeumt ihren eigenen in jedem Fall wieder weg -
    // auch im Fehlerfall, deshalb steht der Aufruf dort im finally.
    private static int ArbeitsordnerImTemp()
        => Directory.EnumerateDirectories(Path.GetTempPath(), "ausgabenverwaltung-pruefung-*")
            .Count(pfad => !Path.GetFileName(pfad).StartsWith(
                "ausgabenverwaltung-pruefung-tests-", StringComparison.Ordinal));

    // ================= Der gute Fall =================

    [Fact]
    public void Eine_gueltige_Sicherung_ist_lesbar_und_wird_durchgezaehlt()
    {
        var zip = ErzeugeSicherung("gut.zip", buchungen: 3);

        var vorher = ArbeitsordnerImTemp();
        var ergebnis = BackupVerification.Verify(zip);

        Assert.True(ergebnis.IsReadable);
        Assert.Equal(DatabaseInitializer.ExpectedSchemaVersion, ergebnis.SchemaVersion);
        Assert.Equal(3, ergebnis.ExpenseCount);
        Assert.False(ergebnis.IsSchemaTooNew);
        Assert.Null(ergebnis.Finding);

        Assert.Equal(vorher, ArbeitsordnerImTemp());
    }

    [Fact]
    public void Das_Merkmal_an_der_Zeile_nennt_Schema_und_Anzahl()
    {
        var ergebnis = BackupVerification.Verify(ErzeugeSicherung("merkmal.zip", buchungen: 1));

        var merkmal = BackupVerificationText.Merkmal(ergebnis);

        Assert.Contains("geprüft", merkmal);
        Assert.Contains($"Schema {DatabaseInitializer.ExpectedSchemaVersion}", merkmal);
        Assert.Contains("1 Buchung", merkmal);
    }

    // ================= Die drei Schadensfaelle =================

    [Fact]
    public void Eine_Sicherung_ohne_Datenbankdatei_ist_nicht_lesbar()
    {
        var zip = Path.Combine(_tempDir.FullName, "ohne-db.zip");
        using (var archiv = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            var eintrag = archiv.CreateEntry("liesmich.txt");
            using var schreiber = new StreamWriter(eintrag.Open());
            schreiber.Write("Hier ist alles, nur keine Datenbank.");
        }

        var vorher = ArbeitsordnerImTemp();
        var ergebnis = BackupVerification.Verify(zip);

        Assert.False(ergebnis.IsReadable);
        Assert.Equal(StorageProblem.DatabaseCorrupt, ergebnis.Problem);
        Assert.NotNull(ergebnis.Finding);

        Assert.Equal(vorher, ArbeitsordnerImTemp());
    }

    [Fact]
    public void Eine_beschaedigte_Datenbank_wird_erkannt()
    {
        var zip = Path.Combine(_tempDir.FullName, "kaputt.zip");
        using (var archiv = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            var eintrag = archiv.CreateEntry(AppPaths.DatabaseFileName);
            using var schreiber = new StreamWriter(eintrag.Open());
            schreiber.Write("SQLite format 3 steht hier gerade nicht.");
        }

        var vorher = ArbeitsordnerImTemp();
        var ergebnis = BackupVerification.Verify(zip);

        Assert.False(ergebnis.IsReadable);
        Assert.Equal(StorageProblem.DatabaseCorrupt, ergebnis.Problem);
        Assert.Equal("nicht lesbar", BackupVerificationText.Merkmal(ergebnis));

        Assert.Equal(vorher, ArbeitsordnerImTemp());
    }

    [Fact]
    public void Eine_Sicherung_aus_einer_neueren_Programmversion_faellt_auf()
    {
        // Lesbar, unbeschaedigt - und trotzdem nicht zurueckzuspielen.
        // Von aussen sieht man ihr das nicht an, deshalb ein eigener
        // Befund und nicht einfach "in Ordnung".
        var neuereVersion = DatabaseInitializer.ExpectedSchemaVersion + 1;
        var zip = ErzeugeSicherung("zu-neu.zip", buchungen: 0, schemaVersion: neuereVersion);

        var vorher = ArbeitsordnerImTemp();
        var ergebnis = BackupVerification.Verify(zip);

        Assert.True(ergebnis.IsReadable);
        Assert.True(ergebnis.IsSchemaTooNew);
        Assert.Equal(neuereVersion, ergebnis.SchemaVersion);
        Assert.Contains("neuere Programmversion", BackupVerificationText.Merkmal(ergebnis));

        Assert.Equal(vorher, ArbeitsordnerImTemp());
    }

    [Fact]
    public void Eine_gar_nicht_vorhandene_Datei_wirft_nicht()
    {
        var vorher = ArbeitsordnerImTemp();

        var ergebnis = BackupVerification.Verify(
            Path.Combine(_tempDir.FullName, "gibtesnicht.zip"));

        Assert.False(ergebnis.IsReadable);
        Assert.NotNull(ergebnis.Finding);

        Assert.Equal(vorher, ArbeitsordnerImTemp());
    }

    // ================= Hilfsmittel =================

    // Baut eine echte Sicherung: eine ueber DatabaseInitializer angelegte
    // Datenbank, als ZIP verpackt wie BackupService es tut - dieselbe
    // Datei im Archiv, derselbe Name.
    private string ErzeugeSicherung(string zipName, int buchungen, int? schemaVersion = null)
    {
        var dbPfad = Path.Combine(_tempDir.FullName, Path.GetFileNameWithoutExtension(zipName) + ".db");

        using (var connection = SqliteConnectionFactory.OpenConnection(
                   $"Data Source={dbPfad};Pooling=False"))
        {
            DatabaseInitializer.Initialize(connection);

            if (schemaVersion is int version)
            {
                connection.Execute(
                    """
                    INSERT INTO SchemaVersion (Version, AppliedUtc)
                    VALUES (@Version, strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
                    """,
                    new { Version = version });
            }

            if (buchungen > 0)
            {
                // Initialize legt nur das Schema an, keine Grunddaten -
                // die entstehen sonst beim ersten Start. Fuer die Zaehlung
                // reichen eine Kategorie und eine Person.
                connection.Execute(
                    """
                    INSERT INTO Person (Name, IsSelf, CreatedUtc)
                    VALUES ('Ich', 1, strftime('%Y-%m-%dT%H:%M:%SZ', 'now'));

                    INSERT INTO Category (ParentId, Name, CreatedUtc)
                    VALUES (NULL, 'Allgemein', strftime('%Y-%m-%dT%H:%M:%SZ', 'now'));
                    """);

                for (var i = 0; i < buchungen; i++)
                {
                    connection.Execute(
                        """
                        INSERT INTO Expense (CategoryId, PayerId, ExpenseDate, AmountCents,
                                             IsIncome, CreatedUtc, ModifiedUtc)
                        SELECT
                            (SELECT MIN(Id) FROM Category),
                            (SELECT MIN(Id) FROM Person),
                            '2026-08-07', 1000, 0,
                            strftime('%Y-%m-%dT%H:%M:%SZ', 'now'),
                            strftime('%Y-%m-%dT%H:%M:%SZ', 'now')
                        """);
                }
            }
        }

        var zipPfad = Path.Combine(_tempDir.FullName, zipName);
        using (var archiv = ZipFile.Open(zipPfad, ZipArchiveMode.Create))
        {
            archiv.CreateEntryFromFile(dbPfad, AppPaths.DatabaseFileName, CompressionLevel.Optimal);
        }

        return zipPfad;
    }
}
