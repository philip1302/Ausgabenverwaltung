using Ausgabenverwaltung.Core.Errors;

namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Ergebnis eines Sicherungslaufs. Rein berichtend - der
/// <see cref="BackupService"/> wirft nicht, weil eine gescheiterte
/// Sicherung den Programmstart nicht verhindern darf. Was davon angezeigt
/// wird, entscheidet die Oberflaeche.
/// </summary>
public sealed record BackupResult
{
    /// <summary>Dateiname der erzeugten Sicherung, sonst NULL.</summary>
    public string? FileName { get; init; }

    public required BackupOutcome Primary { get; init; }

    /// <summary>
    /// Der Text der Ausnahme - technisch, englisch, fuer Protokoll und
    /// aufklappbaren Bereich. NICHT fuer den Haupttext einer Meldung; der
    /// entsteht aus <see cref="PrimaryProblem"/> ueber
    /// <see cref="FileErrorText"/>.
    /// </summary>
    public string? PrimaryError { get; init; }

    /// <summary>
    /// Die Art des Problems, bereits eingeordnet. Steht hier und nicht
    /// erst in der Oberflaeche, weil die Ausnahme selbst hier zuletzt
    /// greifbar ist - danach existiert nur noch dieses Ergebnis.
    /// </summary>
    public StorageProblem PrimaryProblem { get; init; }

    public required BackupOutcome External { get; init; }
    public string? ExternalError { get; init; }
    public StorageProblem ExternalProblem { get; init; }

    /// <summary>Ob ueberhaupt eine neue Sicherungsdatei entstanden ist.</summary>
    public bool CreatedBackup => Primary == BackupOutcome.Succeeded;

    /// <summary>
    /// Der Fall, ueber den die Oberflaeche berichten MUSS: das feste erste
    /// Ziel hat nicht funktioniert, es gibt also gar keine Sicherung. Ein
    /// nicht erreichbares zweites Ziel taucht hier bewusst nicht auf - das
    /// steht still in den Einstellungen.
    /// </summary>
    public bool NeedsAttention => Primary == BackupOutcome.Failed;

    public static BackupResult SkippedToday() => new()
    {
        Primary = BackupOutcome.Skipped,
        External = BackupOutcome.Skipped,
    };
}
