namespace Ausgabenverwaltung.Core.Categories;

/// <summary>
/// Was ein Zusammenfuehren bewegen wuerde - ermittelt VOR dem Ausfuehren,
/// damit in der Bestaetigung steht, worum es geht. Der Vorgang ist nicht
/// rueckgaengig zu machen; eine Vorschau ist die einzige Gelegenheit,
/// einen Irrtum zu bemerken.
/// </summary>
public sealed record CategoryMergePreview
{
    public required int ExpenseCount { get; init; }

    public required int RecurringExpenseCount { get; init; }

    /// <summary>
    /// Summe der umzuhaengenden Ausgaben in Cent (Regel 1). Sie aendert
    /// sich durch das Zusammenfuehren nicht - sie wandert nur von der
    /// einen Kategorie zur anderen.
    /// </summary>
    public required long SumCents { get; init; }
}
