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

    /// <summary>
    /// Woran die Sicherung scheiterte - technisch, fuer Protokoll und
    /// aufklappbaren Bereich. Bewusst nicht im Meldungstext: dort stuende
    /// sonst eine englische Dateisystemmeldung mitten im deutschen Satz.
    /// </summary>
    public string? BackupError { get; }

    // Der Text wird dem Anwender im Startfehlerfenster gezeigt und ist
    // deshalb - anders als die uebrigen Ausnahmemeldungen im Code - in
    // ordentlichem Deutsch mit Umlauten geschrieben. Der technische Grund
    // steht bewusst NICHT darin; der landet im aufklappbaren Bereich.
    public MigrationBackupFailedException(int fromVersion, int toVersion, string? error)
        : base(
            "Die Datenbank muss auf den Aufbau dieser Programmversion umgestellt werden. " +
            "Vorher wird immer eine Sicherung angelegt, weil die Umstellung sich sonst " +
            "nicht mehr zurücknehmen ließe. Genau diese Sicherung ist fehlgeschlagen.\n\n" +
            "Die Umstellung wurde deshalb gar nicht erst begonnen — Ihre Daten sind " +
            "unverändert und vollständig.\n\n" +
            "Bitte prüfen Sie, ob der Sicherungsordner beschreibbar ist und genug Platz " +
            "hat, und starten Sie die Anwendung dann erneut.\n\n" +
            $"Aufbau der Datenbank: Version {fromVersion}, erwartet wird Version {toVersion}.")
    {
        FromVersion = fromVersion;
        ToVersion = toVersion;
        BackupError = error;
    }
}
