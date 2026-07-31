using Ausgabenverwaltung.Core.Backups;

namespace Ausgabenverwaltung.Core.Database;

/// <summary>
/// Wird geworfen, wenn eine faellige Schema-Migration abbricht.
///
/// Zu diesem Zeitpunkt ist die Datenbank bereits wieder unveraendert: jeder
/// Schritt laeuft in einer eigenen Transaktion, und SQLite nimmt auch DDL
/// zurueck (siehe <see cref="DatabaseMigrator"/>). Diese Ausnahme meldet
/// also keinen halben Stand, sondern einen abgelehnten Versuch - und
/// traegt die Sicherung mit, die unmittelbar davor angelegt wurde, damit
/// die Meldung sagen kann, wo der Rueckweg liegt.
///
/// Nicht zu verwechseln mit <see cref="MigrationBackupFailedException"/>:
/// dort ist die Migration gar nicht erst losgelaufen, weil die Sicherung
/// davor schon scheiterte.
/// </summary>
public sealed class MigrationFailedException : Exception
{
    public int FromVersion { get; }
    public int ToVersion { get; }

    /// <summary>Die Sicherung, die vor dem Versuch angelegt wurde.</summary>
    public BackupResult Backup { get; }

    public MigrationFailedException(
        int fromVersion, int toVersion, BackupResult backup, Exception? innerException)
        : base(
            $"Die Umstellung der Datenbank von Version {fromVersion} auf {toVersion} "
            + "ist fehlgeschlagen und wurde zurueckgerollt. Die Daten sind unveraendert.",
            innerException)
    {
        FromVersion = fromVersion;
        ToVersion = toVersion;
        Backup = backup;
    }
}
