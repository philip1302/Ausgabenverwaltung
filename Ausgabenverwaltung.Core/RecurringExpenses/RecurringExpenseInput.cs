namespace Ausgabenverwaltung.Core.RecurringExpenses;

/// <summary>
/// Die rohe Eingabe des Vorlagenformulars, so wie sie im Formular steht -
/// Betrag und Datumsangaben also noch als Text. Wird von
/// <see cref="RecurringExpenseValidator"/> geprueft und umgewandelt.
///
/// Bewusst der ungeparste Zustand: nur so laesst sich die vollstaendige
/// Pruefung eines Formulars ohne Oberflaeche testen (Regel 7). Das Parsen
/// selbst liegt ohnehin in Core (<see cref="Money.TryParseEuroText"/>,
/// <see cref="Formatting.GermanDateInput.TryParse"/>).
///
/// record und nicht class: ein reiner Datentraeger ohne Verhalten, und in
/// den Tests laesst sich so mit "with" ein einzelnes Feld verbiegen, ohne
/// alle uebrigen erneut hinzuschreiben.
/// </summary>
public sealed record RecurringExpenseInput
{
    public required string Title { get; init; }

    /// <summary>NULL = noch keine Kategorie gewaehlt.</summary>
    public int? CategoryId { get; init; }

    /// <summary>NULL = noch kein Zahler gewaehlt.</summary>
    public int? PayerId { get; init; }

    /// <summary>
    /// Ob der gewaehlte Zahler die eigene Person ist (siehe
    /// Expenses.ExpenseInput.PayerIsSelf - dieselbe Pruefung, dieselbe
    /// Begruendung).
    /// </summary>
    public bool PayerIsSelf { get; init; }

    public required string AmountText { get; init; }

    /// <summary>
    /// Ob der Betrag als Einnahme gilt (siehe Expenses.ExpenseInput.IsIncome
    /// - dieselbe Pruefung, dieselbe Begruendung).
    /// </summary>
    public bool IsIncome { get; init; }

    /// <summary>'day' | 'week' | 'month' | 'year'.</summary>
    public required string IntervalUnit { get; init; }

    public required string IntervalCountText { get; init; }

    /// <summary>
    /// Nur bei 'month' und 'year' ausgewertet, sonst ignoriert. Leer ist
    /// zulaessig - dann gilt der Tag des Startdatums, genau wie im
    /// <see cref="RecurrenceGenerator"/>.
    /// </summary>
    public string? AnchorDayText { get; init; }

    public required string StartDateText { get; init; }

    /// <summary>Leer = unbefristet.</summary>
    public string? EndDateText { get; init; }
}
