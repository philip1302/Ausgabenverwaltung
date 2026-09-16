namespace Ausgabenverwaltung.Core.Expenses;

/// <summary>
/// Der Beglichen-Stand einer Buchung, so wie er VOR einer Aenderung war -
/// der Vorrat, aus dem sich ein Abhaken zuruecknehmen laesst (siehe
/// <see cref="ExpenseRepository.RestoreSettledDates"/>).
///
/// Bewusst nur Id und Datum und nicht die ganze
/// <see cref="Ausgabenverwaltung.Core.Entities.Expense"/>: eine Ruecknahme
/// soll genau dieses eine Feld zurueckschreiben und nichts sonst.
/// <c>SettledDate = NULL</c> heisst dabei "war vorher offen" (Regel 4).
/// </summary>
public sealed record SettledState(int Id, DateOnly? SettledDate);
