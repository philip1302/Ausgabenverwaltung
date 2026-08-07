using System.IO.Compression;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Logging;
using Dapper;

namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Prueft, ob eine Sicherungsdatei wirklich taugt.
///
/// Der Grund fuer diese Klasse: eine Sicherung ist nur so viel wert wie
/// ihre Wiederherstellbarkeit. Eine Liste von Dateinamen belegt gar
/// nichts - ein ZIP kann halb geschrieben, die Datenbank darin
/// beschaedigt oder aus einer neueren Programmversion sein. Erst wer die
/// Datei einmal geoeffnet und durchgezaehlt hat, weiss es.
///
/// Ablauf:
/// 1. ZIP in einen eigenen temporaeren Ordner entpacken.
/// 2. Die enthaltene <c>ausgaben.db</c> oeffnen - ueber
///    <see cref="SqliteConnectionFactory"/>, also mit
///    <c>PRAGMA foreign_keys = ON</c> (Regel 2).
/// 3. <c>PRAGMA integrity_check</c> auswerten
///    (<see cref="DatabaseHealth.IntegrityCheck"/>).
/// 4. Schema-Stand und Anzahl der Buchungen lesen.
/// 5. Den temporaeren Ordner in jedem Fall wieder aufraeumen.
///
/// Sie wirft nicht: jedes Problem kommt als
/// <see cref="BackupVerificationResult"/> zurueck.
/// </summary>
public static class BackupVerification
{
    private const string TempFolderPrefix = "ausgabenverwaltung-pruefung-";

    public static BackupVerificationResult Verify(string zipPath)
    {
        var tempFolder = Path.Combine(
            Path.GetTempPath(), TempFolderPrefix + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(tempFolder);
            ZipFile.ExtractToDirectory(zipPath, tempFolder);

            var databasePath = Path.Combine(tempFolder, AppPaths.DatabaseFileName);
            if (!File.Exists(databasePath))
            {
                return Unlesbar(
                    StorageProblem.DatabaseCorrupt,
                    $"Die Sicherung enthaelt keine Datei namens {AppPaths.DatabaseFileName}.");
            }

            return Pruefe(databasePath);
        }
        catch (Exception ex)
        {
            AppLog.Current.Exception("Beim Pruefen einer Sicherung", ex);

            return Unlesbar(StorageProblems.Classify(ex), ex.Message);
        }
        finally
        {
            RaeumeAuf(tempFolder);
        }
    }

    private static BackupVerificationResult Pruefe(string databasePath)
    {
        // Ohne Verbindungspool: sonst haelt der Treiber die Datei noch
        // offen, wenn der temporaere Ordner geloescht werden soll, und
        // unter Windows bliebe er dauerhaft liegen.
        using var connection = SqliteConnectionFactory.OpenConnection(
            $"Data Source={databasePath};Pooling=False");

        if (DatabaseHealth.IntegrityCheck(connection) is { } befund)
        {
            return Unlesbar(StorageProblem.DatabaseCorrupt, befund);
        }

        // Eine Datei ohne lesbaren Schema-Stand ist zwar technisch eine
        // Datenbank, aber keine dieser Anwendung - zum Zurueckspielen
        // taugt sie ebenso wenig wie eine beschaedigte.
        if (DatabaseInitializer.ReadSchemaVersion(connection) is not int version)
        {
            return Unlesbar(
                StorageProblem.DatabaseCorrupt,
                "In der Datei steht kein lesbarer Schema-Stand.");
        }

        var anzahl = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Expense");

        return new BackupVerificationResult
        {
            IsReadable = true,
            SchemaVersion = version,
            ExpenseCount = anzahl,
            IsSchemaTooNew = version > DatabaseInitializer.ExpectedSchemaVersion,
        };
    }

    private static BackupVerificationResult Unlesbar(StorageProblem problem, string befund)
        => new()
        {
            IsReadable = false,
            Problem = problem,
            Finding = befund,
        };

    // Auch im Fehlerfall - deshalb steht der Aufruf im finally. Scheitert
    // das Aufraeumen selbst, bleibt ein Ordner im Temp-Verzeichnis liegen;
    // das raeumt das Betriebssystem irgendwann selbst weg und ist kein
    // Grund, die Pruefung als gescheitert zu melden.
    private static void RaeumeAuf(string tempFolder)
    {
        try
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, recursive: true);
            }
        }
        catch (Exception)
        {
        }
    }
}
