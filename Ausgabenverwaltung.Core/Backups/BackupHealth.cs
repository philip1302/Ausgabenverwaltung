namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Der Gesamtzustand der Datensicherung - EINE Aussage aus beiden Zielen.
///
/// Warum ueberhaupt zusammengefasst: die Seite beantwortet genau eine
/// Frage ("bin ich abgesichert?"). Solange jedes Ziel seine eigene
/// Teilaussage traegt, muss der Anwender sie selbst zu einem Gesamtbild
/// verrechnen - und rechnet dabei erfahrungsgemaess falsch, weil der
/// ernstere Fall (Ziel 1 fehlgeschlagen) optisch neben dem harmloseren
/// (Ziel 2 fehlt) steht.
/// </summary>
public enum BackupHealthLevel
{
    /// <summary>Beide Ziele aktuell.</summary>
    Gesichert,

    /// <summary>
    /// Ziel 1 in Ordnung, aber Ziel 2 fehlt, ist fehlgeschlagen oder
    /// aelter als <see cref="ExternalBackupAge.StaleAfterDays"/>. Es wird
    /// gesichert - nur liegt alles auf einer Platte.
    /// </summary>
    Eingeschraenkt,

    /// <summary>
    /// Ziel 1 ist fehlgeschlagen ODER es gibt gar keine Sicherung. Der
    /// einzige Zustand, der eine Warnfarbe verdient.
    /// </summary>
    NichtGesichert,
}

/// <summary>
/// Der ausgewertete Gesamtzustand samt der Einzelheiten, aus denen er
/// entstanden ist.
///
/// Die Regeln stehen hier und nicht im ViewModel (Regel 7) - eine Ampel,
/// die sich nicht pruefen laesst, zeigt frueher oder spaeter das Falsche
/// an. Die Reihenfolge der Pruefung ist Absicht:
///
/// 1. <b>NichtGesichert</b>: Ziel 1 ist fehlgeschlagen ODER es gibt
///    ueberhaupt keine Sicherung. Auch dann, wenn Ziel 2 aktuell ist -
///    ein zweites Ziel ohne erstes bedeutet, dass der letzte dorthin
///    kopierte Stand beliebig alt sein kann.
/// 2. <b>Eingeschraenkt</b>: Ziel 1 laeuft, Ziel 2 nicht (fehlt, nicht
///    angekommen oder ueberaltert).
/// 3. <b>Gesichert</b>: beides aktuell.
/// </summary>
public sealed record BackupHealth
{
    public required BackupHealthLevel Level { get; init; }

    /// <summary>Zeitpunkt der letzten Sicherung in Ziel 1 (lokale Zeit,
    /// aus dem Dateinamen) - NULL, wenn es keine gibt.</summary>
    public DateTime? LastLocalBackup { get; init; }

    /// <summary>Ob ueberhaupt ein zweites Ziel eingetragen ist.</summary>
    public bool ExternalConfigured { get; init; }

    /// <summary>Letzte erfolgreiche Sicherung in Ziel 2, UTC.</summary>
    public DateTime? LastExternalBackupUtc { get; init; }

    /// <summary>Der letzte Lauf hat Ziel 1 nicht geschrieben.</summary>
    public bool PrimaryFailed { get; init; }

    /// <summary>Der letzte Lauf hat Ziel 2 nicht erreicht.</summary>
    public bool ExternalFailed { get; init; }

    /// <summary>Ziel 2 ist eingerichtet, aber zu lange nicht beschrieben
    /// worden (<see cref="ExternalBackupAge.StaleAfterDays"/>).</summary>
    public bool ExternalStale { get; init; }

    /// <summary>
    /// Wertet den Gesamtzustand aus.
    /// </summary>
    /// <param name="lastRun">Ergebnis des letzten Sicherungslaufs -
    /// NULL, wenn in dieser Sitzung noch keiner lief (etwa beim ersten
    /// Start).</param>
    /// <param name="backupCount">Anzahl vorhandener Sicherungen in Ziel 1.</param>
    /// <param name="lastLocalBackup">Zeitpunkt der neuesten davon.</param>
    /// <param name="externalConfigured">Ob ein zweites Ziel eingetragen ist.</param>
    /// <param name="lastExternalBackupUtc">Letzte erfolgreiche externe Sicherung.</param>
    /// <param name="nowUtc">Jetzt, fuer die Alterung von Ziel 2.</param>
    public static BackupHealth Evaluate(
        BackupResult? lastRun,
        int backupCount,
        DateTime? lastLocalBackup,
        bool externalConfigured,
        DateTime? lastExternalBackupUtc,
        DateTime nowUtc)
    {
        var primaryFailed = lastRun?.Primary == BackupOutcome.Failed;
        var externalFailed = externalConfigured && lastRun?.External == BackupOutcome.Failed;

        // Ohne eingerichtetes Ziel 2 gibt es nichts, das veralten koennte -
        // "fehlt" und "veraltet" sind zwei verschiedene Aussagen, und beide
        // gleichzeitig zu behaupten waere nur verwirrend.
        var externalStale = externalConfigured
            && ExternalBackupAge.IsStale(lastExternalBackupUtc, nowUtc);

        var level = (primaryFailed || backupCount == 0) switch
        {
            true => BackupHealthLevel.NichtGesichert,
            false when !externalConfigured || externalFailed || externalStale
                => BackupHealthLevel.Eingeschraenkt,
            false => BackupHealthLevel.Gesichert,
        };

        return new BackupHealth
        {
            Level = level,
            LastLocalBackup = lastLocalBackup,
            ExternalConfigured = externalConfigured,
            LastExternalBackupUtc = lastExternalBackupUtc,
            PrimaryFailed = primaryFailed,
            ExternalFailed = externalFailed,
            ExternalStale = externalStale,
        };
    }
}
