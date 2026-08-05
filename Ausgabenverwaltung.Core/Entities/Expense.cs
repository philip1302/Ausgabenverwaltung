namespace Ausgabenverwaltung.Core.Entities;

/// <summary>
/// Eine einzelne Ausgabenbuchung.
/// </summary>
public sealed class Expense
{
    public int Id { get; set; }
    public int CategoryId { get; set; }

    /// <summary>Betrag in Cent. Negative Werte = Erstattung.</summary>
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
    /// war). Eine Einnahme MINDERT die Summen in Liste und Auswertung,
    /// statt sie zu erhoehen - unabhaengig vom Vorzeichen von
    /// AmountCents, das weiterhin ausschliesslich "Erstattung" bedeutet.
    /// Beide Konzepte schliessen sich aus: ExpenseValidator lehnt einen
    /// negativen Betrag bei IsIncome = true ab.
    /// </summary>
    public bool IsIncome { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime ModifiedUtc { get; set; }
}
