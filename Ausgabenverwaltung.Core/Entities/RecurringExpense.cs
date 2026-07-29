namespace Ausgabenverwaltung.Core.Entities;

/// <summary>
/// Vorlage fuer wiederkehrende Ausgaben. AmountCents wird beim Erzeugen
/// einer Buchung kopiert, nicht referenziert (Vorlagenaenderungen duerfen
/// die Historie nicht rueckwirkend veraendern).
/// </summary>
public sealed class RecurringExpense
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public int PayerId { get; set; }
    public long AmountCents { get; set; }
    public string? Note { get; set; }
    public string Title { get; set; } = string.Empty;

    /// <summary>'day' | 'week' | 'month' | 'year', siehe CHECK-Constraint in der DB.</summary>
    public string IntervalUnit { get; set; } = string.Empty;
    public int IntervalCount { get; set; }

    /// <summary>Faelligkeitstag im Monat (1..31), nur bei month/year relevant.</summary>
    public int? AnchorDay { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    /// <summary>Bis zu diesem Datum wurden Vorkommen bereits erzeugt. NULL = noch nie gelaufen.</summary>
    public DateOnly? GeneratedThrough { get; set; }

    public bool IsActive { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ModifiedUtc { get; set; }
}
