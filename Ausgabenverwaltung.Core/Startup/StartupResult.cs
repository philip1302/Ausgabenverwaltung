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

    public int GeneratedExpenseCount => GeneratedExpenses.Count;
}
