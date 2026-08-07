namespace Ausgabenverwaltung.Core.Expenses;

/// <summary>
/// Die Werte der zuletzt gleichlautend bemerkten Buchung - das Angebot,
/// das die Erfassungsmaske macht, sobald eine Bemerkung getippt ist
/// (siehe <see cref="ExpenseRepository.SuggestFor"/>).
///
/// Ein Angebot, keine Vorbelegung: eingesetzt wird erst auf Klick. Eine
/// Maske, die sich beim Tippen von selbst fuellt, ueberschreibt frueher
/// oder spaeter etwas, das schon richtig war.
///
/// Datum und Beglichen-Status stehen bewusst nicht darin: das Datum ist
/// der heutige Tag (oder was im Feld steht), und der Beglichen-Status
/// gehoert der einzelnen Buchung (Regel 4).
/// </summary>
public sealed record ExpenseSuggestion
{
    public required int CategoryId { get; init; }

    /// <summary>Voller Pfad ("Pferde › Hufschmied") fuer den Angebotstext.</summary>
    public required string CategoryFullPath { get; init; }

    public required long AmountCents { get; init; }

    public required int PayerId { get; init; }

    public required string PayerName { get; init; }

    /// <summary>
    /// Gehoert zum Angebot, obwohl es kein Feld ist, das der Anwender
    /// gesucht hat: eine Einnahme mit fremdem Zahler wuerde sonst beim
    /// Uebernehmen still zur Ausgabe an dieselbe Person.
    /// </summary>
    public required bool IsIncome { get; init; }

    /// <summary>
    /// Wann die Buchung war, aus der das Angebot stammt. Nur zur Anzeige -
    /// uebernommen wird das Datum nie.
    /// </summary>
    public required DateOnly ExpenseDate { get; init; }
}
