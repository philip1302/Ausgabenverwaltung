using Ausgabenverwaltung.Core;

namespace Ausgabenverwaltung.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData(420000, 4200.00)]
    [InlineData(1, 0.01)]
    [InlineData(0, 0.00)]
    [InlineData(-500, -5.00)]
    public void ToDecimal_wandelt_Cent_korrekt_um(long cents, decimal expected)
    {
        Assert.Equal(expected, Money.ToDecimal(cents));
    }

    [Theory]
    [InlineData(4200.00, 420000)]
    [InlineData(0.01, 1)]
    [InlineData(0.00, 0)]
    [InlineData(-5.00, -500)]
    [InlineData(1.005, 101)] // kaufmaennisch aufgerundet
    public void ToCents_wandelt_Decimal_korrekt_um(decimal amount, long expected)
    {
        Assert.Equal(expected, Money.ToCents(amount));
    }

    [Fact]
    public void Rundtrip_ist_stabil()
    {
        const long cents = 123456;
        var amount = Money.ToDecimal(cents);
        Assert.Equal(cents, Money.ToCents(amount));
    }

    [Theory]
    [InlineData("12,50", 1250)]
    [InlineData("12", 1200)]
    [InlineData("0,01", 1)]
    [InlineData("-5,00", -500)] // Erstattung
    [InlineData(" 12,50 ", 1250)]
    public void TryParseEuroText_erkennt_gueltige_Betraege(string text, long expectedCents)
    {
        var erfolgreich = Money.TryParseEuroText(text, out var cents);

        Assert.True(erfolgreich);
        Assert.Equal(expectedCents, cents);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    // Punkt wird bewusst NICHT als Tausendertrennzeichen akzeptiert,
    // damit "12.50" nicht still zu 1250 statt zu einem Fehler wird.
    [InlineData("12.50")]
    [InlineData("1.234,56")]
    [InlineData("12,50,00")]
    public void TryParseEuroText_lehnt_ungueltige_Eingabe_ab(string text)
    {
        var erfolgreich = Money.TryParseEuroText(text, out var cents);

        Assert.False(erfolgreich);
        Assert.Equal(0, cents);
    }
}
