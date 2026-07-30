namespace Ausgabenverwaltung.Core.Database;

/// <summary>
/// Wird beim Start geworfen, wenn vor einer faelligen Schema-Migration
/// keine Sicherung geschrieben werden konnte.
///
/// Die Anwendung laeuft in diesem Fall bewusst NICHT weiter: eine
/// Migration ist der einzige Vorgang, der die Datenbank baulich
/// veraendert, und der einzige, der sich ohne Sicherung nicht mehr
/// zuruecknehmen laesst. Lieber ein Start, der erklaert, was fehlt, als
/// eine Umstellung, hinter der es kein Zurueck gibt.
/// </summary>
public sealed class MigrationBackupFailedException : Exception
{
    public int FromVersion { get; }
    public int ToVersion { get; }

    public MigrationBackupFailedException(int fromVersion, int toVersion, string? error)
        : base(
            $"Die Datenbank muss von Schema-Version {fromVersion} auf {toVersion} " +
            "umgestellt werden. Vorher wird eine Sicherung angelegt - genau die ist " +
            $"fehlgeschlagen:\n\n{error}\n\n" +
            "Die Umstellung wurde deshalb nicht ausgefuehrt, die Daten sind unveraendert. " +
            "Bitte pruefen, ob der Sicherungsordner beschreibbar ist und genug Platz hat, " +
            "und die Anwendung erneut starten.")
    {
        FromVersion = fromVersion;
        ToVersion = toVersion;
    }
}
