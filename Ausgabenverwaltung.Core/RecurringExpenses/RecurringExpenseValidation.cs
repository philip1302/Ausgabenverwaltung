namespace Ausgabenverwaltung.Core.RecurringExpenses;

/// <summary>
/// Ergebnis der Formularpruefung: je Feld eine Meldung oder NULL, dazu bei
/// fehlerfreier Eingabe die umgewandelten Werte. Es werden immer ALLE
/// Felder geprueft und nicht beim ersten Fehler abgebrochen, damit der
/// Anwender nicht mehrfach hintereinander speichern muss, um alle
/// Beanstandungen zu sehen.
/// </summary>
public sealed class RecurringExpenseValidation
{
    public string? TitleError { get; init; }
    public string? CategoryError { get; init; }
    public string? PayerError { get; init; }
    public string? AmountError { get; init; }
    public string? IntervalCountError { get; init; }
    public string? AnchorDayError { get; init; }
    public string? StartDateError { get; init; }
    public string? EndDateError { get; init; }

    public bool IsValid =>
        TitleError is null
        && CategoryError is null
        && PayerError is null
        && AmountError is null
        && IntervalCountError is null
        && AnchorDayError is null
        && StartDateError is null
        && EndDateError is null;

    // Die folgenden Werte sind nur belegt, wenn IsValid gilt - bei
    // fehlerhafter Eingabe stehen hier Nullwerte, die niemand auswerten
    // darf.
    public long AmountCents { get; init; }
    public int IntervalCount { get; init; }
    public int? AnchorDay { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
}
