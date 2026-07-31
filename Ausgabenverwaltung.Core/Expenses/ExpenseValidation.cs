namespace Ausgabenverwaltung.Core.Expenses;

/// <summary>
/// Ergebnis der Formularpruefung einer Ausgabe: je Feld eine Meldung oder
/// NULL, dazu bei fehlerfreier Eingabe die umgewandelten Werte.
///
/// Es werden immer ALLE Felder geprueft und nicht beim ersten Fehler
/// abgebrochen, damit der Anwender nicht mehrfach hintereinander speichern
/// muss, um alle Beanstandungen zu sehen - dieselbe Ueberlegung wie in
/// <see cref="RecurringExpenses.RecurringExpenseValidation"/>.
/// </summary>
public sealed class ExpenseValidation
{
    public string? AmountError { get; init; }
    public string? CategoryError { get; init; }
    public string? PayerError { get; init; }
    public string? DateError { get; init; }

    /// <summary>
    /// Die Rueckfrage bei einem ungewoehnlichen, aber moeglichen Datum
    /// (siehe <see cref="Formatting.DatePlausibility"/>). KEIN Fehler:
    /// <see cref="IsValid"/> bleibt davon unberuehrt. Das Formular zeigt
    /// die Frage und speichert erst nach einer Bestaetigung.
    /// </summary>
    public string? DateConfirmation { get; init; }

    public bool IsValid =>
        AmountError is null
        && CategoryError is null
        && PayerError is null
        && DateError is null;

    /// <summary>Ob vor dem Speichern noch etwas zu bestaetigen ist.</summary>
    public bool NeedsConfirmation => DateConfirmation is not null;

    // Nur belegt, wenn IsValid gilt - bei fehlerhafter Eingabe stehen hier
    // Nullwerte, die niemand auswerten darf.
    public long AmountCents { get; init; }
    public DateOnly Date { get; init; }
}
