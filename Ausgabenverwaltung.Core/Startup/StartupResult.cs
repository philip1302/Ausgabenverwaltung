using Ausgabenverwaltung.Core.Backups;
using Ausgabenverwaltung.Core.Entities;

namespace Ausgabenverwaltung.Core.Startup;

/// <summary>
/// Ergebnis eines Programmstarts. Rein informativ - die Oberflaeche
/// entscheidet selbst, was sie damit anzeigt.
/// </summary>
public sealed class StartupResult
{
    public required string DatabaseFilePath { get; init; }
    public required bool IsFirstStart { get; init; }
    public required IReadOnlyList<Expense> GeneratedExpenses { get; init; }

    /// <summary>
    /// Ergebnis des Sicherungslaufs. NULL beim allerersten Start, wo es
    /// nichts zu sichern gab.
    /// </summary>
    public BackupResult? Backup { get; init; }

    /// <summary>
    /// Ergebnis der Schema-Migration. NULL, wenn nichts umzustellen war -
    /// bei jedem Start ausser dem einen nach einem Programm-Update.
    /// </summary>
    public MigrationResult? Migration { get; init; }

    public int GeneratedExpenseCount => GeneratedExpenses.Count;
}
