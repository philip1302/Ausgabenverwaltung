namespace Ausgabenverwaltung.Core.Expenses;

/// <summary>
/// Eine Ausgabe fuer Uebersichtslisten (z. B. "letzte Ausgaben" in der
/// Erfassungsmaske): bereits mit Kategorie- und Zahlername verbunden,
/// damit die Oberflaeche nicht selbst nachschlagen muss.
/// </summary>
public sealed class ExpenseOverview
{
    public required int Id { get; init; }
    public required DateOnly ExpenseDate { get; init; }
    public required long AmountCents { get; init; }
    public required string CategoryName { get; init; }
    public required string PayerName { get; init; }
    public string? Note { get; init; }

    /// <summary>Ob dieser Betrag eine Einnahme ist (siehe Entities.Expense.IsIncome).</summary>
    public required bool IsIncome { get; init; }
}
