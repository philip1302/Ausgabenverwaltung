namespace Ausgabenverwaltung.Core.Expenses;

/// <summary>
/// Sortierbare Spalten der Ausgabenliste. Sortiert wird in SQL (siehe
/// <see cref="ExpenseRepository.Query"/>) - dieser Aufzaehlungstyp ist
/// zugleich die Weissliste erlaubter ORDER-BY-Ausdruecke, es gelangt also
/// nie Anwendereingabe in die Abfrage.
/// </summary>
public enum ExpenseSortColumn
{
    Datum,
    Kategorie,
    Betrag,
    Zahler,
    Status,
    Bemerkung,
}
