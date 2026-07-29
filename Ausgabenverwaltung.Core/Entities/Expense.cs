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

    public DateTime CreatedUtc { get; set; }
    public DateTime ModifiedUtc { get; set; }
}
