using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Das Betragsfeld rechnet: mehrere Posten eines Belegs lassen sich
/// zusammenzaehlen, ohne den Taschenrechner zu bemuehen.
/// </summary>
public class BetragsAusdruckTests
{
    [Theory]
    [InlineData("12,50", 1250)]
    [InlineData("12", 1200)]
    [InlineData("0,01", 1)]
    [InlineData(" 12,50 ", 1250)]
    public void Eine_einzelne_Zahl_wird_gelesen_wie_bisher(string text, long erwartet)
    {
        Assert.Equal(erwartet, BetragsAusdruck.Auswerten(text));
    }

    [Theory]
    [InlineData("12,50+3,20", 1570)]
    [InlineData("10+10+10", 3000)]
    [InlineData("12,50 + 3,20", 1570)] // Leerzeichen sind erlaubt
    public void Summen_werden_zusammengezaehlt(string text, long erwartet)
    {
        Assert.Equal(erwartet, BetragsAusdruck.Auswerten(text));
    }

    [Theory]
    [InlineData("40-2,50", 3750)]
    [InlineData("100-10-10", 8000)]
    [InlineData("40 - 2,50", 3750)]
    public void Differenzen_werden_abgezogen(string text, long erwartet)
    {
        Assert.Equal(erwartet, BetragsAusdruck.Auswerten(text));
    }

    [Fact]
    public void Ein_negatives_Ergebnis_kommt_als_negativer_Wert_zurueck()
    {
        // Dass Betraege immer positiv sind, ist eine Fachregel und steht
        // an genau einer Stelle - im ExpenseValidator, siehe den Test
        // darunter. Waere sie zusaetzlich hier, lieferte die Auswertung
        // NULL und aus der genauen Meldung wuerde ein pauschales "kein
        // gueltiger Betrag".
        Assert.Equal(-2500L, BetragsAusdruck.Auswerten("15-40"));
    }

    [Fact]
    public void Ein_negatives_Ergebnis_wird_vom_Validator_abgelehnt()
    {
        var ergebnis = ExpenseValidator.Validate(new ExpenseInput
        {
            AmountText = "15-40",
            CategoryId = 1,
            PayerId = 1,
            DateText = "31.07.2026",
            Today = new DateOnly(2026, 7, 31),
        });

        Assert.False(ergebnis.IsValid);
        Assert.Equal("Der Betrag darf nicht negativ sein.", ergebnis.AmountError);
    }

    [Theory]
    [InlineData("12.50", 1250)]
    [InlineData("12.5", 1250)]
    [InlineData("12.50+3.20", 1570)]
    public void Der_Punkt_gilt_als_Dezimaltrennzeichen(string text, long erwartet)
    {
        // Anders als Money.TryParseEuroText, das den Punkt pauschal
        // ablehnt: hier ist er eindeutig, weil eine Tausendergruppe immer
        // dreistellig ist. Was verstanden wurde, schreibt die Oberflaeche
        // ueber Normalform sofort zurueck.
        Assert.Equal(erwartet, BetragsAusdruck.Auswerten(text));
    }

    [Theory]
    [InlineData("1.500")]   // koennte 1500 oder 1,50 meinen - bleibt abgelehnt
    [InlineData("1.234,56")] // Tausenderpunkt UND Komma
    public void Eine_mehrdeutige_Punktstellung_wird_abgelehnt(string text)
    {
        Assert.Null(BetragsAusdruck.Auswerten(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("12,,5")]
    [InlineData("12,50,00")]
    [InlineData("12,50+")]     // angefangen, aber nicht fertig
    [InlineData("12,50++3")]
    [InlineData("+")]
    [InlineData("12,50*2")]    // Multiplikation gibt es bewusst nicht
    [InlineData("(12+3)")]
    [InlineData("99999999999999999999999999999")] // laeuft nicht ueber, sondern faellt durch
    public void Unsinn_liefert_null_statt_einer_Ausnahme(string text)
    {
        Assert.Null(BetragsAusdruck.Auswerten(text));
    }

    [Fact]
    public void Auswerten_vertraegt_null()
    {
        Assert.Null(BetragsAusdruck.Auswerten(null));
    }

    [Theory]
    [InlineData("12,50+3,20", "15,70")]
    [InlineData("12.5", "12,50")]
    [InlineData("12,50", "12,50")]
    public void Normalform_zeigt_was_verstanden_wurde(string text, string erwartet)
    {
        Assert.Equal(erwartet, BetragsAusdruck.Normalform(text));
    }

    [Fact]
    public void Normalform_liefert_null_wenn_nichts_zurueckzuschreiben_ist()
    {
        // Dann bleibt stehen, was der Anwender getippt hat - eine halb
        // fertige Eingabe darf ihm nicht unter den Fingern verschwinden.
        Assert.Null(BetragsAusdruck.Normalform("12,50+"));
    }
}
