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
        Id = expense.Id;

        var datumText = GermanDateInput.ToText(expense.ExpenseDate);
        VorText = datumText + "  ·  ";

        BetragText = EuroText.FormatSigned(expense.AmountCents, expense.IsIncome);

        // Kurzbeschreibung fuer den Hinweis-Chip der Ausgabenliste nach
        // dem Sprung auf diese Buchung. An EINER Stelle zusammengesetzt,
        // damit die beiden Aufrufer (Startseite und Erfassen) nicht
        // auseinanderlaufen. Bewusst mit einfachen Trennpunkten statt der
        // gepolsterten Fassung aus VorText: der Chip ist eine Zeile Text,
        // keine Tabellenspalte.
        Beschreibung = $"{datumText} · {BetragText} · {expense.CategoryName}";

        IstOffen = !expense.PayerIsSelf && expense.SettledDate is null;
        IstEinnahme = !expense.PayerIsSelf && expense.SettledDate is not null && expense.IsIncome;
        IstBeglichenAusgabe =
            !expense.PayerIsSelf && expense.SettledDate is not null && !expense.IsIncome;

        NachText = $"  ·  {expense.CategoryName}  ·  {expense.PayerName}"
            + (string.IsNullOrWhiteSpace(expense.Note) ? string.Empty : $"  ·  {expense.Note}");
    }

    /// <summary>
    /// Die Buchung selbst - fuer den Sprung in die Ausgabenliste, die
    /// danach genau diese eine Zeile zeigt.
    /// </summary>
    public int Id { get; }

    /// <summary>Datum, Betrag und Kategorie in einer Zeile.</summary>
    public string Beschreibung { get; }

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
