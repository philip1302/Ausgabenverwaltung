using Ausgabenverwaltung.Core.Display;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.OpenItems;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Das gemeinsame Verhalten sortierbarer Spaltenueberschriften. Vorher
/// stand es zweimal abgeschrieben in den ViewModels und war dort nicht
/// pruefbar; die Faelle hier gelten jetzt fuer jede Liste, die
/// <see cref="Sortierung"/> benutzt.
/// </summary>
public class SortierungTests
{
    // ---------------- Spalte aus dem Kommandoparameter ----------------

    [Fact]
    public void Der_Kommandoparameter_trifft_seinen_Aufzaehlungswert()
    {
        Assert.Equal(
            ExpenseSortColumn.Bemerkung,
            Sortierung.Spalte<ExpenseSortColumn>("Bemerkung"));

        Assert.Equal(
            OpenItemsSortColumn.TageOffen,
            Sortierung.Spalte<OpenItemsSortColumn>("TageOffen"));
    }

    [Fact]
    public void Ein_unbekannter_Name_liefert_nichts()
    {
        Assert.Null(Sortierung.Spalte<ExpenseSortColumn>("Waehrung"));
        Assert.Null(Sortierung.Spalte<ExpenseSortColumn>(null));
        Assert.Null(Sortierung.Spalte<ExpenseSortColumn>(string.Empty));
    }

    // Eine Spalte der einen Liste ist kein gueltiger Name in der anderen:
    // "Zahler" gibt es in der Ausgabenliste, bei den offenen Posten nicht.
    [Fact]
    public void Eine_fremde_Spalte_gilt_nicht()
    {
        Assert.Null(Sortierung.Spalte<OpenItemsSortColumn>("Zahler"));
    }

    // Enum.TryParse nimmt auch Zahlen an und liefert fuer "99" ein
    // klagloses true mit einem Wert, den es nicht gibt. Der wuerde bis in
    // die ORDER-BY-Weissliste von ExpenseRepository durchlaufen und dort
    // werfen - deshalb faellt er schon hier durch.
    [Fact]
    public void Eine_Zahl_ist_keine_Spalte()
    {
        Assert.Null(Sortierung.Spalte<ExpenseSortColumn>("99"));
        Assert.Null(Sortierung.Spalte<ExpenseSortColumn>("2"));
    }

    // Gross- und Kleinschreibung zaehlt: die Parameter stehen so in den
    // Ansichten wie die Werte in der Aufzaehlung.
    [Fact]
    public void Kleingeschrieben_gilt_nicht()
    {
        Assert.Null(Sortierung.Spalte<ExpenseSortColumn>("datum"));
    }

    // ---------------- Richtung ----------------

    [Fact]
    public void Dieselbe_Spalte_dreht_die_Richtung_um()
    {
        var (spalte, aufsteigend) = Sortierung.NaechsteRichtung(
            ExpenseSortColumn.Datum, ExpenseSortColumn.Datum, aktuellAufsteigend: true);

        Assert.Equal(ExpenseSortColumn.Datum, spalte);
        Assert.False(aufsteigend);

        var (_, wiederAufsteigend) = Sortierung.NaechsteRichtung(
            ExpenseSortColumn.Datum, ExpenseSortColumn.Datum, aktuellAufsteigend: false);

        Assert.True(wiederAufsteigend);
    }

    // Auch dann aufsteigend, wenn die vorige Spalte absteigend stand: die
    // Richtung gehoert zur Spalte und nicht zur Tabelle.
    [Fact]
    public void Eine_andere_Spalte_faengt_immer_aufsteigend_an()
    {
        var (spalte, aufsteigend) = Sortierung.NaechsteRichtung(
            ExpenseSortColumn.Betrag, ExpenseSortColumn.Datum, aktuellAufsteigend: false);

        Assert.Equal(ExpenseSortColumn.Betrag, spalte);
        Assert.True(aufsteigend);
    }

    // ---------------- Beschriftung ----------------

    [Fact]
    public void Nur_die_aktive_Spalte_traegt_ein_Dreieck()
    {
        Assert.Equal(
            "Betrag ▲",
            Sortierung.KopfText(
                "Betrag", ExpenseSortColumn.Betrag, ExpenseSortColumn.Betrag, aufsteigend: true));

        Assert.Equal(
            "Betrag ▼",
            Sortierung.KopfText(
                "Betrag", ExpenseSortColumn.Betrag, ExpenseSortColumn.Betrag, aufsteigend: false));

        Assert.Equal(
            "Betrag",
            Sortierung.KopfText(
                "Betrag", ExpenseSortColumn.Betrag, ExpenseSortColumn.Datum, aufsteigend: true));
    }

    // Das Dreieck steht NEBEN der Bezeichnung und nicht an ihrer Stelle -
    // dieselbe Ueberlegung wie bei den Kategoriefarben (Regel 10).
    [Fact]
    public void Das_Dreieck_ersetzt_die_Bezeichnung_nie()
    {
        foreach (var spalte in Enum.GetValues<ExpenseSortColumn>())
        {
            var text = Sortierung.KopfText("Datum", spalte, spalte, aufsteigend: true);
            Assert.StartsWith("Datum", text);
        }
    }
}
