using System.Text;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Core.Startup;

/// <summary>
/// Uebersetzt eine Ausnahme aus <see cref="StartupService.Run(string)"/> in
/// einen fertigen <see cref="StartupFailure"/>.
///
/// Die einzige Stelle, die entscheidet, WIE ein Startabbruch dem Anwender
/// erklaert wird. Sie liegt in Core und nicht in der Oberflaeche, damit
/// sich jeder dieser Texte pruefen laesst, ohne ein Fenster zu oeffnen
/// (Regel 7) - und weil es genau eine solche Stelle geben soll: ein
/// zweiter Ort mit einer eigenen Formulierung fuer denselben Fall waere
/// der Anfang widerspruechlicher Meldungen.
///
/// Es gibt hier keinen Zweig, der nur "Ein Fehler ist aufgetreten" sagt.
/// Auch der Auffangzweig fuer Unbekanntes nennt Folge und naechsten
/// Schritt.
/// </summary>
public static class StartupFailureText
{
    public static StartupFailure Describe(
        Exception exception,
        string databaseFilePath,
        string backupFolderPath,
        string? logFolderPath,
        string version)
    {
        var failure = Beschreibe(exception, databaseFilePath, backupFolderPath, logFolderPath);

        return failure with
        {
            Technical = BaueTechnik(exception, databaseFilePath, version, logFolderPath),
        };
    }

    /// <summary>
    /// Es laeuft bereits eine Ausfuehrung, die sich aber nicht meldet.
    /// Kein Fehler im eigentlichen Sinn und deshalb ohne Ausnahme - aber
    /// ein Fall, in dem der Anwender vor einem Programm steht, das nicht
    /// aufgeht, und eine Erklaerung braucht.
    /// </summary>
    public static StartupFailure AlreadyRunning(string? logFolderPath)
        => new()
        {
            Title = "Die Ausgabenverwaltung läuft bereits.",
            Message =
                "Die Anwendung ist schon geöffnet. Sie kann nur einmal laufen, weil "
                + "sonst zwei Fenster dieselbe Datei beschreiben würden.\n\n"
                + "Das vorhandene Fenster sollte eigentlich nach vorn geholt werden, "
                + "hat aber nicht geantwortet. Vermutlich ist es gerade beschäftigt "
                + "oder wird gerade beendet.\n\n"
                + "Es wurde nichts verändert. Bitte sehen Sie in der Taskleiste nach "
                + "dem offenen Fenster. Finden Sie keines, warten Sie einen Moment und "
                + "starten Sie die Anwendung dann erneut.",
            Technical =
                "Es wurde bereits eine laufende Instanz erkannt (benanntes Mutex), "
                + "die Benachrichtigung ueber die benannte Pipe blieb jedoch ohne "
                + "Antwort. Einzelheiten dazu stehen im Protokoll.",
            FolderPath = logFolderPath,
            FolderButtonText = logFolderPath is null ? null : "Protokollordner öffnen",
            RetryWorthwhile = true,
        };

    // Der Kern: welcher Fall liegt vor und was ist dazu zu sagen. Die
    // technischen Angaben haengt Describe an, damit sie hier nicht in
    // jedem Zweig wiederholt werden muessen.
    private static StartupFailure Beschreibe(
        Exception exception,
        string databaseFilePath,
        string backupFolderPath,
        string? logFolderPath)
    {
        switch (exception)
        {
            case DatabaseCorruptException:
                return new StartupFailure
                {
                    Title = "Die gespeicherten Daten sind beschädigt.",
                    Message = DatabaseErrorText.Corrupt(databaseFilePath, backupFolderPath),
                    Technical = string.Empty,
                    FolderPath = backupFolderPath,
                    FolderButtonText = "Sicherungsordner öffnen",
                    RetryWorthwhile = false,
                };

            case SchemaVersionUnreadableException:
                return new StartupFailure
                {
                    Title = "Die gespeicherten Daten sind unvollständig.",
                    Message = DatabaseErrorText.SchemaUnreadable(databaseFilePath, backupFolderPath),
                    Technical = string.Empty,
                    FolderPath = backupFolderPath,
                    FolderButtonText = "Sicherungsordner öffnen",
                    RetryWorthwhile = false,
                };

            case SchemaVersionTooNewException tooNew:
                return new StartupFailure
                {
                    Title = "Die Daten stammen aus einer neueren Programmversion.",
                    Message = DatabaseErrorText.SchemaTooNew(
                        tooNew.ActualVersion, tooNew.ExpectedVersion),
                    Technical = string.Empty,

                    // Hier gibt es nichts wiederherzustellen - die Daten
                    // sind in Ordnung, nur die Programmversion ist die
                    // falsche. Der Protokollordner ist das Sinnvollere.
                    FolderPath = logFolderPath,
                    FolderButtonText = logFolderPath is null ? null : "Protokollordner öffnen",
                    RetryWorthwhile = false,
                };

            case MigrationFailedException migration:
                return new StartupFailure
                {
                    Title = "Die Datenbank konnte nicht aktualisiert werden.",
                    Message = DatabaseErrorText.MigrationFailed(
                        migration.FromVersion,
                        migration.ToVersion,
                        backupFolderPath,
                        migration.Backup.FileName),
                    Technical = string.Empty,
                    FolderPath = backupFolderPath,
                    FolderButtonText = "Sicherungsordner öffnen",

                    // Ein erneuter Versuch lohnt: war die Ursache
                    // voruebergehend (kurz gesperrte Datei, kurz voller
                    // Datentraeger), laeuft die Umstellung beim naechsten
                    // Start durch.
                    RetryWorthwhile = true,
                };

            case MigrationBackupFailedException backupFailed:
                return new StartupFailure
                {
                    Title = "Vor der Aktualisierung ließ sich nichts sichern.",

                    // Die Ausnahme traegt ihren Text bereits mit und
                    // beschreibt genau diesen Fall.
                    Message = backupFailed.Message + "\n\n"
                        + $"Sicherungsordner:\n{backupFolderPath}",
                    Technical = string.Empty,
                    FolderPath = backupFolderPath,
                    FolderButtonText = "Sicherungsordner öffnen",
                    RetryWorthwhile = true,
                };
        }

        // Kein eigener Ausnahmetyp - dann entscheidet die Art des
        // Speicherproblems. ClassifyForFile und nicht Classify: nur mit
        // dem Blick auf die Datei laesst sich eine belegte von einer
        // verschwundenen unterscheiden (siehe dort).
        var problem = StorageProblems.ClassifyForFile(exception, databaseFilePath);

        return problem switch
        {
            StorageProblem.DatabaseLocked => new StartupFailure
            {
                Title = "Die Anwendung läuft möglicherweise bereits.",
                Message = DatabaseErrorText.Locked(databaseFilePath),
                Technical = string.Empty,
                FolderPath = logFolderPath,
                FolderButtonText = logFolderPath is null ? null : "Protokollordner öffnen",
                RetryWorthwhile = true,
            },

            StorageProblem.DatabaseCorrupt => new StartupFailure
            {
                Title = "Die gespeicherten Daten sind beschädigt.",
                Message = DatabaseErrorText.Corrupt(databaseFilePath, backupFolderPath),
                Technical = string.Empty,
                FolderPath = backupFolderPath,
                FolderButtonText = "Sicherungsordner öffnen",
                RetryWorthwhile = false,
            },

            StorageProblem.PathNotFound => new StartupFailure
            {
                Title = "Die Datei mit den Ausgaben ist nicht erreichbar.",
                Message =
                    "Die Anwendung findet die Datei mit den erfassten Ausgaben nicht. "
                    + "Möglicherweise ist ein Wechseldatenträger abgezogen, ein "
                    + "Netzlaufwerk nicht verbunden oder ein Ordner umbenannt "
                    + "worden.\n\n"
                    + "Es wurde nichts verändert und nichts gelöscht — die Datei ist "
                    + "nur gerade nicht zu erreichen.\n\n"
                    + "Bitte stellen Sie die Verbindung wieder her und starten Sie die "
                    + "Anwendung erneut. Ist die Datei tatsächlich verloren, liegt im "
                    + "Sicherungsordner der letzte gesicherte Stand.\n\n"
                    + $"Erwarteter Ort:\n{databaseFilePath}",
                Technical = string.Empty,
                FolderPath = backupFolderPath,
                FolderButtonText = "Sicherungsordner öffnen",
                RetryWorthwhile = true,
            },

            StorageProblem.AccessDenied or StorageProblem.ReadOnly => new StartupFailure
            {
                Title = "Die Datei mit den Ausgaben darf nicht geöffnet werden.",
                Message =
                    "Die Anwendung darf nicht auf die Datei mit den erfassten Ausgaben "
                    + "zugreifen. Möglicherweise haben sich die Berechtigungen des "
                    + "Ordners geändert, oder ein Virenschutz greift ein.\n\n"
                    + "Es wurde nichts verändert — die Daten sind unberührt.\n\n"
                    + "Bitte prüfen Sie die Berechtigungen des Ordners und starten Sie "
                    + "die Anwendung erneut.\n\n"
                    + $"Betroffene Datei:\n{databaseFilePath}",
                Technical = string.Empty,
                FolderPath = logFolderPath,
                FolderButtonText = logFolderPath is null ? null : "Protokollordner öffnen",
                RetryWorthwhile = true,
            },

            StorageProblem.DiskFull => new StartupFailure
            {
                Title = "Auf dem Datenträger ist kein Platz mehr frei.",
                Message =
                    "Der Start ist abgebrochen, weil auf dem Datenträger kein Platz "
                    + "mehr frei ist.\n\n"
                    + "Es wurde nichts verändert — die bereits erfassten Ausgaben sind "
                    + "vollständig vorhanden.\n\n"
                    + "Bitte schaffen Sie etwas Platz und starten Sie die Anwendung "
                    + "dann erneut.",
                Technical = string.Empty,
                FolderPath = logFolderPath,
                FolderButtonText = logFolderPath is null ? null : "Protokollordner öffnen",
                RetryWorthwhile = true,
            },

            // Auffangzweig. Auch er nennt Folge und naechsten Schritt -
            // "Ein Fehler ist aufgetreten" allein gibt es nicht.
            _ => new StartupFailure
            {
                Title = "Die Anwendung konnte nicht gestartet werden.",
                Message =
                    "Beim Start ist ein Fehler aufgetreten, mit dem die Anwendung "
                    + "nicht gerechnet hat. Sie bricht deshalb ab, statt in einem "
                    + "unklaren Zustand weiterzulaufen.\n\n"
                    + "Es wurde nichts gespeichert und nichts verändert — die bereits "
                    + "erfassten Ausgaben sind unberührt.\n\n"
                    + "Bitte starten Sie die Anwendung noch einmal. Bleibt es dabei, "
                    + "stehen die Einzelheiten unten zum Aufklappen und Kopieren "
                    + (logFolderPath is null
                        ? "bereit."
                        : $"bereit — und im Protokoll:\n{logFolderPath}"),
                Technical = string.Empty,
                FolderPath = logFolderPath,
                FolderButtonText = logFolderPath is null ? null : "Protokollordner öffnen",
                RetryWorthwhile = true,
            },
        };
    }

    private static string BaueTechnik(
        Exception exception, string databaseFilePath, string version, string? logFolderPath)
    {
        var text = new StringBuilder();

        text.AppendLine($"Zeitpunkt:  {IsoDateTime.ToUtcText(DateTime.UtcNow)}");
        text.AppendLine($"Version:    {version}");
        text.AppendLine($"Datenbank:  {databaseFilePath}");

        if (logFolderPath is not null)
        {
            text.AppendLine($"Protokoll:  {logFolderPath}");
        }

        // Woran die Sicherung vor einer Migration scheiterte, steht nicht
        // im Text der Ausnahme (dort waere es eine englische
        // Dateisystemmeldung mitten im deutschen Satz) - hier gehoert es
        // hin.
        if (exception is MigrationBackupFailedException { BackupError: { } backupError })
        {
            text.AppendLine($"Sicherung:  {backupError}");
        }

        text.AppendLine();
        text.Append(exception.ToString());

        return text.ToString();
    }
}
