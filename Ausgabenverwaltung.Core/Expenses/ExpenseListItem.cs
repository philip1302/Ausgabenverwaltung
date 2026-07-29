namespace Ausgabenverwaltung.Core.Expenses;

/// <summary>
/// Eine Zeile der Ausgabenliste: die Buchung samt Zahlername, vollem
/// Kategoriepfad und - falls sie aus einer Vorlage erzeugt wurde - dem
/// Titel dieser Vorlage. Bereits fertig verbunden aus der Datenbank
/// geladen (siehe <see cref="ExpenseRepository.Query"/>), damit die
/// Anzeige nichts nachschlagen muss.
/// </summary>
public sealed class ExpenseListItem
{
    public required int Id { get; init; }
    public required DateOnly ExpenseDate { get; init; }

    public required int CategoryId { get; init; }
    public required string CategoryFullPath { get; init; }

    /// <summary>Betrag in Cent. Negative Werte = Erstattung.</summary>
    public required long AmountCents { get; init; }

    public required int PayerId { get; init; }
    public required string PayerName { get; init; }

    /// <summary>
    /// Ob der Zahler die eigene Person ist. Noetig fuer die Statusanzeige:
    /// bei eigenen Ausgaben wird SettledDate nie ausgewertet (Regel 4).
    /// </summary>
    public required bool PayerIsSelf { get; init; }

    /// <summary>NULL = nicht beglichen (nur bei fremdem Zahler relevant).</summary>
    public DateOnly? SettledDate { get; init; }

    public string? Note { get; init; }

    /// <summary>NULL = von Hand erfasst, sonst aus dieser Vorlage erzeugt.</summary>
    public int? RecurringExpenseId { get; init; }

    /// <summary>
    /// Titel der erzeugenden Vorlage, NULL bei handerfassten Buchungen.
    /// Kann auch dann NULL sein, wenn die Vorlage inzwischen geloescht
    /// wurde - der Fremdschluessel steht auf ON DELETE SET NULL, die
    /// Buchung bleibt also ohne Vorlage zurueck.
    /// </summary>
    public string? RecurringExpenseTitle { get; init; }
}
