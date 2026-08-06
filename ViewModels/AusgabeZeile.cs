using System;
using Ausgabenverwaltung.Anzeige;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Ausgabenverwaltung.ViewModels;

/// <summary>
/// Eine Zeile der Ausgabenliste: Anzeigewerte aus
/// <see cref="ExpenseListItem"/> plus der Auswahlzustand fuer das
/// Sammel-Loeschen. Bewusst getrennt von der Core-Entitaet, damit diese
/// keinen UI-Zustand tragen muss (Regel 7).
/// </summary>
public sealed partial class AusgabeZeile : ObservableObject
{
    public int Id { get; }
    public DateOnly ExpenseDate { get; }
    public string DatumText { get; }
    public int CategoryId { get; }
    public string CategoryFullPath { get; }
    public long AmountCents { get; }
    public string BetragText { get; }

    /// <summary>
    /// Der Buchungstyp selbst - unabhaengig von Zahler und Beglichen-
    /// Status. Fuer Stellen ausserhalb der Farbwahl, die genau das
    /// brauchen (etwa das Bearbeiten-Formular, das die Checkbox
    /// "Einnahme" danach vorbelegt) - NICHT fuer die Farbe: siehe
    /// <see cref="IstBeglicheneEinnahme"/>.
    /// </summary>
    public bool IstEinnahme { get; }

    /// <summary>
    /// Beglichene Einnahme mit fremdem Zahler - tatsaechlich zugeflossenes
    /// Geld, wird gruen hervorgehoben. Eine eigene Einnahme zaehlt NICHT
    /// dazu (sie gleicht sich aus, Regel 4) und bleibt neutral, ebenso wie
    /// eine noch offene fremde Einnahme (siehe <see cref="IstOffen"/>).
    /// </summary>
    public bool IstBeglicheneEinnahme { get; }

    /// <summary>
    /// Beglichene Ausgabe mit fremdem Zahler - der Anwender hat sie schon
    /// zurueckbekommen, wird blau hervorgehoben.
    /// </summary>
    public bool IstBeglichenAusgabe { get; }

    public int PayerId { get; }
    public string PayerName { get; }
    public bool PayerIsSelf { get; }
    public DateOnly? SettledDate { get; }
    public string? Note { get; }
    public int? RecurringExpenseId { get; }

    /// <summary>
    /// Aus einer Vorlage erzeugt - wird in der Tabelle mit einem Zeichen
    /// markiert und im Bearbeiten-Dialog erklaert (Regel 6: die Historie
    /// haengt nicht an der Vorlage).
    /// </summary>
    public bool IstAusVorlage { get; }

    public string VorlageHinweis { get; }

    /// <summary>
    /// Regel 4: SettledDate bedeutet nur in Kombination mit einem fremden
    /// Zahler etwas. Bei eigenen Ausgaben bleibt die Spalte deshalb leer
    /// statt "offen" zu behaupten.
    /// </summary>
    public string StatusText { get; }

    /// <summary>
    /// Ausgabe ODER Einnahme mit fremdem Zahler, noch nicht beglichen -
    /// wird rot hervorgehoben. Eine eigene Buchung ist nie "offen"
    /// (Regel 4) und bleibt neutral, unabhaengig vom Buchungstyp.
    /// </summary>
    public bool IstOffen { get; }

    /// <summary>
    /// Die aufgeloeste Farbe der Kategorie (siehe
    /// <see cref="Ausgabenverwaltung.Core.Categories.CategoryColors"/>),
    /// als Pinsel fuer den schmalen Balken am linken Zeilenrand. Der
    /// Balken ERGAENZT die Kategoriespalte, er ersetzt sie nicht - ohne
    /// Farbwahrnehmung bleibt die Zeile vollstaendig lesbar.
    /// </summary>
    public IBrush Farbe { get; }

    /// <summary>
    /// Derselbe Wert als '#RRGGBB' - fuer den Bearbeiten-Dialog, der
    /// daraus wieder eine Kategorie-Option baut.
    /// </summary>
    public string FarbeHex { get; }

    [ObservableProperty]
    private bool _istAusgewaehlt;

    public AusgabeZeile(ExpenseListItem item, string kategorieFarbe)
    {
        FarbeHex = kategorieFarbe;
        Farbe = Farbpinsel.Fuer(kategorieFarbe);

        Id = item.Id;
        ExpenseDate = item.ExpenseDate;
        DatumText = GermanDateInput.ToText(item.ExpenseDate);
        CategoryId = item.CategoryId;
        CategoryFullPath = item.CategoryFullPath;
        AmountCents = item.AmountCents;
        BetragText = EuroText.FormatSigned(item.AmountCents, item.IsIncome);
        IstEinnahme = item.IsIncome;
        PayerId = item.PayerId;
        PayerName = item.PayerName;
        PayerIsSelf = item.PayerIsSelf;
        SettledDate = item.SettledDate;
        Note = item.Note;
        RecurringExpenseId = item.RecurringExpenseId;

        IstAusVorlage = item.RecurringExpenseId is not null;
        VorlageHinweis = item.RecurringExpenseTitle is { } titel
            ? $"Aus Vorlage \"{titel}\" erzeugt"
            : IstAusVorlage
                ? "Aus einer inzwischen geloeschten Vorlage erzeugt"
                : string.Empty;

        IstOffen = !item.PayerIsSelf && item.SettledDate is null;
        IstBeglicheneEinnahme = !item.PayerIsSelf && item.SettledDate is not null && item.IsIncome;
        IstBeglichenAusgabe = !item.PayerIsSelf && item.SettledDate is not null && !item.IsIncome;

        StatusText = item.PayerIsSelf
            ? "—"
            : item.SettledDate is DateOnly beglichen
                ? $"{(item.IsIncome ? "erhalten am" : "beglichen am")} {GermanDateInput.ToText(beglichen)}"
                : "offen";
    }

    /// <summary>
    /// Beschreibung der Zeile fuer die Loesch-Sicherheitsabfrage: Datum,
    /// Kategorie und Betrag, damit erkennbar bleibt, was verschwindet.
    /// </summary>
    public string LoeschBeschreibung =>
        $"{DatumText} · {CategoryFullPath} · {BetragText}";
}
