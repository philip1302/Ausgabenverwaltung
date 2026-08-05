namespace Ausgabenverwaltung.Core.Expenses;

/// <summary>
/// Die Rohwerte eines Ausgabenformulars, so wie sie im Formular stehen -
/// Text und noch nicht gewaehlte Auswahlen. Umgewandelt und geprueft wird
/// in <see cref="ExpenseValidator"/>.
///
/// Bewusst dieselbe Bauweise wie
/// <see cref="RecurringExpenses.RecurringExpenseInput"/>: Erfassungsmaske
/// und Bearbeiten-Dialog haben dieselben Felder und muessen dieselben
/// Antworten geben.
/// </summary>
public sealed record ExpenseInput
{
    public required string AmountText { get; init; }

    /// <summary>
    /// Ob der Betrag als Einnahme gilt. Steuert nur die Pruefung hier
    /// (ein negativer Betrag ist bei einer Einnahme kein gueltiger Wert -
    /// siehe ExpenseValidator.PruefeBetrag); das eigentliche Attribut
    /// steht auf Entities.Expense.
    /// </summary>
    public bool IsIncome { get; init; }

    /// <summary>NULL, solange keine Kategorie gewaehlt ist.</summary>
    public int? CategoryId { get; init; }

    /// <summary>NULL, solange kein Zahler gewaehlt ist.</summary>
    public int? PayerId { get; init; }

    public required string DateText { get; init; }

    /// <summary>
    /// Der heutige Tag - fuer die Rueckfrage bei ungewoehnlichen Daten.
    /// Kommt von aussen herein und wird nicht selbst ermittelt, damit sich
    /// die Grenzfaelle pruefen lassen, ohne die Systemuhr zu stellen.
    /// </summary>
    public required DateOnly Today { get; init; }
}
