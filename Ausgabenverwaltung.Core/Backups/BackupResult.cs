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
    public string? PrimaryError { get; init; }

    public required BackupOutcome External { get; init; }
    public string? ExternalError { get; init; }

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
