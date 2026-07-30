using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine Zeile der Liste "Letzte Ausgaben" unter der Erfassungsmaske.
///
/// Bewusst dreigeteilt statt als ein fertiger Text: nur so laesst sich
/// der Betrag als Erstattung hervorheben, ohne die ganze Zeile
/// einzufaerben. Zusammengesetzt wird sie erst in der Ansicht.
/// </summary>
public sealed class LetzteAusgabeZeile
{
    public LetzteAusgabeZeile(ExpenseOverview expense)
    {
        VorText = GermanDateInput.ToText(expense.ExpenseDate) + "  ·  ";

        BetragText = EuroText.Format(expense.AmountCents);
        IstErstattung = EuroText.IsNegative(expense.AmountCents);

        NachText = $"  ·  {expense.CategoryName}  ·  {expense.PayerName}"
            + (string.IsNullOrWhiteSpace(expense.Note) ? string.Empty : $"  ·  {expense.Note}");
    }

    /// <summary>Alles vor dem Betrag - hier nur das Datum.</summary>
    public string VorText { get; }

    public string BetragText { get; }

    public bool IstErstattung { get; }

    /// <summary>Alles nach dem Betrag: Kategorie, Zahler, Bemerkung.</summary>
    public string NachText { get; }
}
