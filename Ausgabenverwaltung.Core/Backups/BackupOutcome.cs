namespace Ausgabenverwaltung.Core.Backups;

/// <summary>
/// Wie ein einzelnes Sicherungsziel ausgegangen ist.
/// </summary>
public enum BackupOutcome
{
    /// <summary>Kein zweites Ziel eingerichtet - gilt nur fuer Ziel 2.</summary>
    NotConfigured,

    /// <summary>Heute wurde bereits gesichert, es lief nichts.</summary>
    Skipped,

    Succeeded,
    Failed,
}
