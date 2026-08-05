namespace Ausgabenverwaltung.Core.Entities;

/// <summary>
/// Eine einzelne Ausgabenbuchung.
/// </summary>
public sealed class Expense
{
    public int Id { get; set; }
    public int CategoryId { get; set; }

    /// <summary>
    /// Betrag in Cent, immer positiv - ExpenseValidator lehnt negative
    /// Eingaben ab. Ob der Betrag die Summen erhoeht oder mindert und mit
    /// welchem Vorzeichen/welcher Farbe er angezeigt wird, ergibt sich
    /// ausschliesslich aus <see cref="IsIncome"/> (siehe
    /// Formatting.EuroText.FormatSigned).
    /// </summary>
    public long AmountCents { get; set; }

    public DateOnly ExpenseDate { get; set; }
    public string? Note { get; set; }
    public int PayerId { get; set; }

    /// <summary>
    /// NULL = offen, ABER nur relevant, wenn PayerId nicht die eigene
    /// Person ist. Bei eigenen Ausgaben bleibt das Feld leer und wird
    /// nie ausgewertet.
    /// </summary>
    public DateOnly? SettledDate { get; set; }

    /// <summary>NULL = von Hand erfasst, sonst aus dieser Vorlage automatisch erzeugt.</summary>
    public int? RecurringExpenseId { get; set; }

    /// <summary>
    /// Ob dieser Betrag eine allgemeine Einnahme ist statt einer Ausgabe
    /// (z. B. ein Gehaltseingang, der vorher nicht als Ausgabe gebucht
    /// war). Eine Einnahme ERHOEHT die Ergebnis-Summen (Liste, Auswertung),
    /// sobald sie beglichen ist (SettledDate gesetzt), eine Ausgabe
    /// MINDERT sie immer - das Vorzeichen kommt ausschliesslich von
    /// diesem Feld, nie vom gespeicherten Vorzeichen von AmountCents
    /// (das immer positiv ist).
    /// </summary>
    public bool IsIncome { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime ModifiedUtc { get; set; }
}
