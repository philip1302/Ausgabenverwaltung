using Ausgabenverwaltung.Core;
using Ausgabenverwaltung.Core.Display;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.OpenItems;
using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die eine Stelle, an der Aufzaehlungswerte aus fremdem Text gelesen
/// werden - aus der Einstellungsdatei und aus den Kommandoparametern der
/// Ansichten. Sie ist strenger als Enum.TryParse, und genau das wird hier
/// festgehalten.
/// </summary>
public class AufzaehlungTests
{
    [Fact]
    public void Der_Name_liefert_seinen_Wert()
    {
        Assert.Equal(
            ExpenseSortColumn.Zahler,
            Aufzaehlung.NachName<ExpenseSortColumn>("Zahler"));
    }

    [Fact]
    public void Nichts_Lesbares_liefert_nichts()
    {
        Assert.Null(Aufzaehlung.NachName<ExpenseSortColumn>(null));
        Assert.Null(Aufzaehlung.NachName<ExpenseSortColumn>(string.Empty));
        Assert.Null(Aufzaehlung.NachName<ExpenseSortColumn>("   "));
        Assert.Null(Aufzaehlung.NachName<ExpenseSortColumn>("Waehrung"));
    }

    // Der eigentliche Anlass fuer diese Klasse: Enum.TryParse liest
    // Zahlen. "99" ergibt dort einen Wert, den es nicht gibt, und "2"
    // trifft zufaellig den dritten Wert der Aufzaehlung - beides soll
    // hier nicht durchkommen.
    [Theory]
    [InlineData("99")]
    [InlineData("2")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    public void Zahlen_werden_nicht_gelesen(string text)
    {
        Assert.Null(Aufzaehlung.NachName<ExpenseSortColumn>(text));
    }

    // Enum.TryParse verodert kommagetrennte Namen (fuer Flags-
    // Aufzaehlungen gedacht). Keine Aufzaehlung dieser Anwendung ist eine,
    // und "Datum, Betrag" ergaebe eine Spalte, die niemand gespeichert hat.
    [Fact]
    public void Zusammengesetzte_Angaben_werden_nicht_gelesen()
    {
        Assert.Null(Aufzaehlung.NachName<ExpenseSortColumn>("Datum, Betrag"));
    }

    [Fact]
    public void Andere_Schreibweise_gilt_nicht()
    {
        Assert.Null(Aufzaehlung.NachName<ExpenseSortColumn>("betrag"));
        Assert.Null(Aufzaehlung.NachName<ExpenseSortColumn>("BETRAG"));
    }

    /// <summary>
    /// Die Zusage, auf der das Speichern der Einstellungen ruht: was
    /// <c>ToString()</c> hinschreibt, liest <c>NachName</c> wieder als
    /// denselben Wert. Ueber ALLE Werte jeder Aufzaehlung, die in der
    /// Einstellungsdatei oder in einem Kommandoparameter vorkommt - ein
    /// spaeter angehaengter Wert ist damit von selbst mitgeprueft.
    /// </summary>
    [Fact]
    public void Was_geschrieben_wird_laesst_sich_wieder_lesen()
    {
        PruefeRundreise<ExpenseSortColumn>();
        PruefeRundreise<OpenItemsSortColumn>();
        PruefeRundreise<ThemeMode>();
        PruefeRundreise<ReportGrouping>();
    }

    private static void PruefeRundreise<T>() where T : struct, Enum
    {
        foreach (var wert in Enum.GetValues<T>())
        {
            Assert.Equal(wert, Aufzaehlung.NachName<T>(wert.ToString()));
        }
    }
}
