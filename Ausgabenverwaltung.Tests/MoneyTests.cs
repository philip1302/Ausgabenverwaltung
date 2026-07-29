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
}
