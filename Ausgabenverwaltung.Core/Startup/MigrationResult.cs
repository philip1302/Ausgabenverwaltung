using Ausgabenverwaltung.Core.Backups;

namespace Ausgabenverwaltung.Core.Startup;

/// <summary>
/// Ergebnis einer beim Start ausgefuehrten Schema-Migration. NULL im
/// Startergebnis bedeutet: es war nichts umzustellen - der Normalfall.
/// </summary>
public sealed class MigrationResult
{
    public required int FromVersion { get; init; }
    public required int ToVersion { get; init; }

    /// <summary>
    /// Die Sicherung, die VOR der Umstellung geschrieben wurde. Sie ist
    /// der Rueckweg, falls sich die neue Programmversion als untauglich
    /// erweist, und deshalb Teil des Ergebnisses.
    /// </summary>
    public required BackupResult Backup { get; init; }
}
