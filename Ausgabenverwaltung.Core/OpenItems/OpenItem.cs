namespace Ausgabenverwaltung.Core.OpenItems;

/// <summary>
/// Eine offene oder kuerzlich beglichene Ausgabe mit fremdem Zahler,
/// bereits mit Personenname und vollem Kategoriepfad fuer die Anzeige in
/// der Offene-Posten-Liste (siehe <see cref="OpenItemsRepository"/>).
/// </summary>
public sealed class OpenItem
{
    public required int Id { get; init; }
    public required DateOnly ExpenseDate { get; init; }
    public required long AmountCents { get; init; }
    public string? Note { get; init; }
    public required int PayerId { get; init; }
    public required string PayerName { get; init; }
    public required string CategoryFullPath { get; init; }

    /// <summary>NULL, solange der Posten noch offen ist.</summary>
    public DateOnly? SettledDate { get; init; }

    /// <summary>
    /// Tage zwischen ExpenseDate und SettledDate, bzw. zwischen
    /// ExpenseDate und heute, solange der Posten noch offen ist.
    /// </summary>
    public required int TageOffen { get; init; }
}
