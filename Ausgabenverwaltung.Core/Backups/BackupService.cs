using System.Data;
using System.IO.Compression;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Settings;
using Dapper;

namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Erzeugt und verwaltet die Sicherungen.
///
/// Ablauf eines Laufs:
/// 1. VACUUM INTO schreibt eine konsistente Kopie in eine temporaere
///    Datei. Die laufende Datenbankdatei wird NICHT kopiert - dabei
///    entstuenden Kopien mitten in einer Transaktion, und die daneben
///    liegenden -wal/-shm-Dateien fehlten.
/// 2. Diese Kopie wird als ZIP komprimiert.
/// 3. Die temporaere Datei wird geloescht.
/// 4. Das ZIP liegt damit in Ziel 1; danach wird es in Ziel 2 kopiert,
///    falls dort ein Pfad eingetragen ist.
///
/// Der Dienst wirft nie. Jeder Fehler landet als Text im
/// <see cref="BackupResult"/>, denn eine gescheiterte Sicherung darf den
/// Programmstart nicht verhindern.
/// </summary>
public sealed class BackupService
{
    // Endung der Zwischendatei aus Schritt 1. Sie faellt durch das Raster
    // von BackupFileName und wird deshalb nie fuer eine Sicherung gehalten.
    private const string TempExtension = ".db.tmp";

    private readonly IDbConnection _connection;
    private readonly AppSettingsStore _settingsStore;

    public BackupService(
        IDbConnection connection,
        string primaryFolderPath,
        AppSettingsStore settingsStore)
    {
        _connection = connection;
        PrimaryFolderPath = primaryFolderPath;
        _settingsStore = settingsStore;
    }

    /// <summary>Das feste erste Ziel.</summary>
    public string PrimaryFolderPath { get; }

    /// <summary>
    /// Sichert, sofern heute noch nicht gesichert wurde - der Lauf beim
    /// Programmstart. Mehrere Starts am selben Tag erzeugen also nur eine
    /// Sicherung. Ob heute schon gesichert wurde, verraet der Zeitstempel
    /// im Dateinamen; es gibt keinen zweiten Ort, an dem das stehen und
    /// veralten koennte.
    /// </summary>
    public BackupResult RunIfDue(DateTime nowLocal)
    {
        try
        {
            var today = DateOnly.FromDateTime(nowLocal);
            if (ReadFolder(PrimaryFolderPath).Any(
                    backup => DateOnly.FromDateTime(backup.Timestamp) == today))
            {
                return BackupResult.SkippedToday();
            }
        }
        catch (Exception)
        {
            // Ordner nicht lesbar: dann lieber sichern als nicht sichern.
            // Der Versuch meldet das eigentliche Problem gleich selbst.
        }

        return RunNow(nowLocal);
    }

    /// <summary>
    /// Sichert unabhaengig davon, ob heute schon gesichert wurde - fuer
    /// "Sicherung jetzt". Ein zweiter Lauf in derselben Minute
    /// ueberschreibt die Datei, weil der Dateiname nur bis zur Minute
    /// genau ist.
    /// </summary>
    public BackupResult RunNow(DateTime nowLocal)
    {
        var fileName = BackupFileName.Create(nowLocal);

        var (primaryPath, primaryError) = WritePrimary(fileName);

        // Aufraeumen erst nach einer erfolgreichen Sicherung: an einem Tag,
        // an dem nichts Neues entstanden ist, sollen auch keine alten
        // Staende verschwinden.
        if (primaryPath is not null)
        {
            ApplyRetention(PrimaryFolderPath);
        }

        var (external, externalError) = CopyToExternal(primaryPath, fileName);

        return new BackupResult
        {
            FileName = primaryPath is null ? null : fileName,
            Primary = primaryPath is null ? BackupOutcome.Failed : BackupOutcome.Succeeded,
            PrimaryError = primaryError,
            External = external,
            ExternalError = externalError,
        };
    }

    /// <summary>
    /// Die vorhandenen Sicherungen in Ziel 1, neueste zuerst.
    /// </summary>
    public IReadOnlyList<BackupFile> ListBackups() => ReadFolder(PrimaryFolderPath);

    // ---------------- Ziel 1 ----------------

    private (string? Path, string? Error) WritePrimary(string fileName)
    {
        string? zipPath = null;

        try
        {
            Directory.CreateDirectory(PrimaryFolderPath);
            RemoveStaleTempFiles();

            // Die Zwischendatei liegt im Sicherungsordner und nicht im
            // Temp-Verzeichnis: derselbe Datentraeger, auf dem das Ergebnis
            // ohnehin Platz braucht.
            var tempPath = Path.Combine(
                PrimaryFolderPath, Path.GetFileNameWithoutExtension(fileName) + TempExtension);

            try
            {
                VacuumInto(tempPath);

                zipPath = Path.Combine(PrimaryFolderPath, fileName);
                Compress(tempPath, zipPath);
            }
            finally
            {
                DeleteQuietly(tempPath);
            }

            return (zipPath, null);
        }
        catch (Exception ex)
        {
            // Ein halb geschriebenes ZIP waere schlimmer als gar keins: es
            // saehe in der Liste wie eine gueltige Sicherung aus.
            DeleteQuietly(zipPath);
            return (null, ex.Message);
        }
    }

    // VACUUM INTO gibt es seit SQLite 3.27. Es schreibt den Inhalt der
    // Datenbank in einem Rutsch in eine neue, aufgeraeumte Datei und sieht
    // dabei einen in sich stimmigen Stand - auch waehrend die Anwendung
    // dieselbe Verbindung weiter benutzt. Die Zieldatei darf nicht
    // existieren, deshalb wird sie vorher weggeraeumt.
    private void VacuumInto(string targetPath)
    {
        DeleteQuietly(targetPath);
        _connection.Execute("VACUUM INTO @TargetPath", new { TargetPath = targetPath });
    }

    // Die Datei im ZIP heisst wie die aktive Datenbank. Wiederherstellen
    // ist dadurch Entpacken und Ersetzen, ohne Umbenennen.
    private static void Compress(string databasePath, string zipPath)
    {
        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(databasePath, AppPaths.DatabaseFileName, CompressionLevel.Optimal);
    }

    // Reste eines abgestuerzten Laufs. Sie liegen sonst dauerhaft herum,
    // weil die Aufbewahrung nur ZIP-Dateien mit passendem Namen anfasst.
    private void RemoveStaleTempFiles()
    {
        foreach (var path in Directory.EnumerateFiles(PrimaryFolderPath, "*" + TempExtension))
        {
            DeleteQuietly(path);
        }
    }

    // ---------------- Ziel 2 ----------------

    private (BackupOutcome Outcome, string? Error) CopyToExternal(string? primaryPath, string fileName)
    {
        var settings = _settingsStore.Load();

        if (string.IsNullOrWhiteSpace(settings.ExternalFolderPath))
        {
            return (BackupOutcome.NotConfigured, null);
        }

        if (primaryPath is null)
        {
            return (BackupOutcome.Failed, "Es wurde keine Sicherung erzeugt, die kopiert werden koennte.");
        }

        try
        {
            Directory.CreateDirectory(settings.ExternalFolderPath);
            File.Copy(primaryPath, Path.Combine(settings.ExternalFolderPath, fileName), overwrite: true);

            ApplyRetention(settings.ExternalFolderPath);

            _settingsStore.Save(settings with { LastExternalBackupUtc = DateTime.UtcNow });

            return (BackupOutcome.Succeeded, null);
        }
        catch (Exception ex)
        {
            // Ziel 2 nicht erreichbar - USB-Stick abgezogen, Netzlaufwerk
            // nicht verbunden, Ordner umbenannt. Kein Abbruch und kein
            // Dialog: Ziel 1 lief ja. Sichtbar wird es ueber den
            // Zeitstempel der letzten erfolgreichen externen Sicherung,
            // der jetzt eben stehen bleibt.
            return (BackupOutcome.Failed, ex.Message);
        }
    }

    // ---------------- Aufbewahrung ----------------

    // Gilt fuer beide Ziele: auch der selbst gewaehlte Ordner soll nicht
    // unbegrenzt wachsen. Angefasst wird ausschliesslich, was exakt dem
    // Namensmuster entspricht - fremde Dateien im Ordner bleiben unberuehrt.
    private static void ApplyRetention(string folderPath)
    {
        try
        {
            foreach (var expired in BackupRetention.SelectExpired(ReadFolder(folderPath)))
            {
                DeleteQuietly(expired.FullPath);
            }
        }
        catch (Exception)
        {
            // Aufraeumen ist Nebensache. Scheitert es, bleibt eine Datei zu
            // viel liegen - das ist kein Grund, den Lauf als
            // fehlgeschlagen zu melden.
        }
    }

    private static IReadOnlyList<BackupFile> ReadFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            return [];
        }

        var backups = new List<BackupFile>();

        foreach (var path in Directory.EnumerateFiles(folderPath, "*.zip"))
        {
            var fileName = Path.GetFileName(path);
            if (!BackupFileName.TryParseTimestamp(fileName, out var timestamp))
            {
                continue;
            }

            backups.Add(new BackupFile(path, fileName, timestamp, new FileInfo(path).Length));
        }

        return backups.OrderByDescending(backup => backup.Timestamp).ToList();
    }

    private static void DeleteQuietly(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception)
        {
            // Gesperrte oder schreibgeschuetzte Datei: nicht der Rede wert,
            // und in keinem Fall ein Grund, den Lauf abzubrechen.
        }
    }
}
