using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine Zeile der Liste "Letzte Ausgaben" unter der Erfassungsmaske (und
/// "Letzte Buchungen" auf der Startseite).
///
/// Bewusst dreigeteilt statt als ein fertiger Text: nur so laesst sich
/// der Betrag hervorheben, ohne die ganze Zeile einzufaerben.
/// Zusammengesetzt wird sie erst in der Ansicht.
///
/// Die Farblogik entspricht der von <see cref="AusgabeZeile"/>: offen
/// (rot), beglichene Einnahme (gruen), beglichene Ausgabe (blau), eigene
/// Buchung gleich welchen Typs (neutral).
/// </summary>
public sealed class LetzteAusgabeZeile
{
    public LetzteAusgabeZeile(ExpenseOverview expense)
    {
        VorText = GermanDateInput.ToText(expense.ExpenseDate) + "  ·  ";

        BetragText = EuroText.FormatSigned(expense.AmountCents, expense.IsIncome);

        IstOffen = !expense.PayerIsSelf && expense.SettledDate is null;
        IstEinnahme = !expense.PayerIsSelf && expense.SettledDate is not null && expense.IsIncome;
        IstBeglichenAusgabe =
            !expense.PayerIsSelf && expense.SettledDate is not null && !expense.IsIncome;

        NachText = $"  ·  {expense.CategoryName}  ·  {expense.PayerName}"
            + (string.IsNullOrWhiteSpace(expense.Note) ? string.Empty : $"  ·  {expense.Note}");
    }

    /// <summary>Alles vor dem Betrag - hier nur das Datum.</summary>
    public string VorText { get; }

    public string BetragText { get; }

    /// <summary>Ausgabe oder Einnahme mit fremdem Zahler, noch nicht
    /// beglichen - wird rot hervorgehoben.</summary>
    public bool IstOffen { get; }

    /// <summary>Beglichene Einnahme mit fremdem Zahler - wird gruen
    /// hervorgehoben.</summary>
    public bool IstEinnahme { get; }

    /// <summary>Beglichene Ausgabe mit fremdem Zahler - wird blau
    /// hervorgehoben.</summary>
    public bool IstBeglichenAusgabe { get; }

    /// <summary>Alles nach dem Betrag: Kategorie, Zahler, Bemerkung.</summary>
    public string NachText { get; }
}
