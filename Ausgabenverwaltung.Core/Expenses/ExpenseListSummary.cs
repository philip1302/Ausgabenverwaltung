namespace Ausgabenverwaltung.Core.Expenses;

/// <summary>
/// Anzahl und Summe der Treffer eines Filters, fuer die Fusszeile der
/// Ausgabenliste. Wird als eigene Aggregatabfrage in SQL ermittelt (siehe
/// <see cref="ExpenseRepository.Summarize"/>) und nicht ueber die
/// geladenen Zeilen gerechnet.
/// </summary>
public sealed record ExpenseListSummary(int Count, long SumCents);
