using Ausgabenverwaltung.Core.Display;

namespace Ausgabenverwaltung.Tests;

public class FontScalesTests
{
    [Fact]
    public void Jede_Stufe_hat_ihren_Faktor_und_ihre_Beschriftung()
    {
        Assert.Equal(0.8, FontScales.Factor(FontScaleStep.Small));
        Assert.Equal(1.0, FontScales.Factor(FontScaleStep.Normal));
        Assert.Equal(1.4, FontScales.Factor(FontScaleStep.Large));
        Assert.Equal(2.0, FontScales.Factor(FontScaleStep.ExtraLarge));

        Assert.Equal("Klein", FontScales.Label(FontScaleStep.Small));
        Assert.Equal("Sehr groß", FontScales.Label(FontScaleStep.ExtraLarge));

        Assert.Equal(4, FontScales.Steps.Count);
        Assert.Equal(FontScales.DefaultFactor, FontScales.Factor(FontScaleStep.Normal));
    }

    [Theory]
    [InlineData(0.8, FontScaleStep.Small)]
    [InlineData(1.0, FontScaleStep.Normal)]
    [InlineData(1.4, FontScaleStep.Large)]
    [InlineData(2.0, FontScaleStep.ExtraLarge)]
    public void Ein_gespeicherter_Faktor_findet_seine_Stufe_wieder(double faktor, FontScaleStep erwartet)
    {
        Assert.Equal(erwartet, FontScales.FromFactor(faktor));
        Assert.Equal(faktor, FontScales.Normalize(faktor));
    }

    [Theory]
    // Zwischenwerte landen bei der naechstgelegenen Stufe - auch die
    // frueheren Faktoren 1,15 und 1,3 aus einer aelteren Einstellungsdatei ...
    [InlineData(1.15, 1.0)]
    [InlineData(1.3, 1.4)]
    [InlineData(0.95, 1.0)]
    // ... Werte ausserhalb bei der kleinsten bzw. groessten ...
    [InlineData(0.1, 0.8)]
    [InlineData(99.0, 2.0)]
    [InlineData(-5.0, 0.8)]
    // ... und ein fehlender Wert (0) ebenfalls bei der kleinsten.
    [InlineData(0.0, 0.8)]
    public void Ein_krummer_Faktor_wird_auf_eine_bekannte_Stufe_gerundet(double gelesen, double erwartet)
    {
        Assert.Equal(erwartet, FontScales.Normalize(gelesen));
    }

    [Fact]
    public void Unbrauchbare_Werte_landen_bei_Normal()
    {
        Assert.Equal(1.0, FontScales.Normalize(double.NaN));
        Assert.Equal(1.0, FontScales.Normalize(double.PositiveInfinity));
    }

    [Fact]
    public void Schriftgroessen_wachsen_mit_dem_Faktor()
    {
        var normal = FontSizes.For(1.0);
        Assert.Equal(11, normal.Small);
        Assert.Equal(14, normal.Normal);
        Assert.Equal(16, normal.Heading);

        var sehrGross = FontSizes.For(2.0);
        Assert.Equal(22, sehrGross.Small);
        Assert.Equal(28, sehrGross.Normal);
        Assert.Equal(32, sehrGross.Heading);

        var klein = FontSizes.For(0.8);
        Assert.Equal(8.8, klein.Small);
        Assert.Equal(11.2, klein.Normal);
        Assert.Equal(12.8, klein.Heading);
    }

    [Fact]
    public void Das_Groessenverhaeltnis_bleibt_auf_jeder_Stufe_erhalten()
    {
        foreach (var stufe in FontScales.Steps)
        {
            var groessen = FontSizes.For(FontScales.Factor(stufe));

            Assert.True(groessen.Small < groessen.Normal);
            Assert.True(groessen.Normal < groessen.Heading);
        }
    }
}
