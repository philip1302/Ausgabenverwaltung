using System.Globalization;
using Ausgabenverwaltung.Core;
using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Tests;

public class EuroTextTests
{
    // Das geschuetzte Leerzeichen zwischen Zahl und Zeichen. In den
    // Erwartungswerten ausgeschrieben, weil es sonst im Quelltext von
    // einem gewoehnlichen Leerzeichen nicht zu unterscheiden waere.
    private const char Geschuetzt = (char)0x00A0;

    [Theory]
    [InlineData(420000, "4.200,00")]
    [InlineData(123456, "1.234,56")]
    [InlineData(1, "0,01")]
    [InlineData(0, "0,00")]
    [InlineData(-12000, "-120,00")]
    [InlineData(-123456789, "-1.234.567,89")]
    public void Format_schreibt_deutsch_mit_Zeichen_hinten(long cents, string zahl)
    {
        Assert.Equal(zahl + Geschuetzt + "€", EuroText.Format(cents));
    }

    [Fact]
    public void Zwischen_Zahl_und_Zeichen_steht_kein_gewoehnliches_Leerzeichen()
    {
        // Sonst koennte der Zeilenumbruch den Betrag von seiner Waehrung
        // trennen.
        Assert.DoesNotContain(" ", EuroText.Format(420000));
    }

    [Theory]
    [InlineData(420000, "4200,00")]
    [InlineData(123456, "1234,56")]
    [InlineData(0, "0,00")]
    [InlineData(-12000, "-120,00")]
    public void Plain_liefert_die_blanke_Zahl_ohne_Zeichen_und_ohne_Tausenderpunkt(
        long cents, string erwartet)
    {
        Assert.Equal(erwartet, EuroText.Plain(cents));
    }

    [Fact]
    public void Plain_laesst_sich_wieder_einlesen()
    {
        // Das ist der Grund fuer den fehlenden Tausenderpunkt: die
        // Eingabefelder werden damit vorbelegt, und Money.TryParseEuroText
        // lehnt Tausendertrennzeichen bewusst ab.
        const long cents = 123456;

        Assert.True(Money.TryParseEuroText(EuroText.Plain(cents), out var gelesen));
        Assert.Equal(cents, gelesen);
    }

    [Fact]
    public void Die_Kultur_haengt_nicht_an_der_Systemeinstellung()
    {
        var vorher = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

            Assert.Equal("4.200,00" + Geschuetzt + "€", EuroText.Format(420000));
            Assert.Equal("4200,00", EuroText.Plain(420000));
        }
        finally
        {
            CultureInfo.CurrentCulture = vorher;
        }
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void IsNegative_erkennt_Erstattungen(long cents, bool erwartet)
    {
        Assert.Equal(erwartet, EuroText.IsNegative(cents));
    }

    [Fact]
    public void Format_stellt_bei_Einnahme_ein_Plus_voran()
    {
        Assert.Equal("+300,00" + Geschuetzt + "€", EuroText.Format(30000, isIncome: true));
    }

    [Fact]
    public void Format_ohne_isIncome_Parameter_bleibt_wie_bisher()
    {
        // Default false: bestehende Aufrufstellen sind unveraendert.
        Assert.Equal("300,00" + Geschuetzt + "€", EuroText.Format(30000));
    }

    [Fact]
    public void Format_stellt_bei_Nullbetrag_und_isIncome_kein_Plus_voran()
    {
        // Ein "+0,00 €" waere irrefuehrend - der Sonderfall existiert in
        // der Praxis nicht (ein Betrag von 0,00 wird abgelehnt), soll die
        // Formatierung aber auch dann nicht verfaelschen.
        Assert.Equal("0,00" + Geschuetzt + "€", EuroText.Format(0, isIncome: true));
    }
}
