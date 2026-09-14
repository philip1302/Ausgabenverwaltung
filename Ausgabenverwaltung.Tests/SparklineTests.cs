using Ausgabenverwaltung.Core.Charts;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Der kleine Verlauf neben einer Kennzahl. Wie beim grossen Diagramm
/// liegt der Ursprung OBEN links - ein groesseres Y heisst weiter unten.
/// </summary>
public class SparklineTests
{
    private const double Breite = 120;
    private const double Hoehe = 28;

    [Fact]
    public void Ohne_Werte_bleibt_die_Sparkline_leer()
    {
        var layout = Sparkline.Compute([], Breite, Hoehe);

        Assert.True(layout.IsEmpty);
        Assert.Empty(layout.Points);
        Assert.Null(layout.Last);
    }

    /// <summary>
    /// Ein einzelner Wert ergibt keine Linie, sondern einen Punkt. Der
    /// wuerde als waagerechter Strich gezeichnet und behauptete eine Ruhe,
    /// die nie gemessen wurde.
    /// </summary>
    [Fact]
    public void Ein_einzelner_Wert_ergibt_noch_keinen_Verlauf()
    {
        var layout = Sparkline.Compute([50000], Breite, Hoehe);

        Assert.True(layout.IsEmpty);
    }

    [Fact]
    public void Jeder_Wert_bekommt_seinen_Punkt()
    {
        var layout = Sparkline.Compute([10000, 20000, 15000, 40000], Breite, Hoehe);

        Assert.False(layout.IsEmpty);
        Assert.Equal(4, layout.Points.Count);
    }

    /// <summary>
    /// Die Linie nutzt die Flaeche ganz aus: links beginnt sie am Rand,
    /// rechts endet sie am Rand.
    /// </summary>
    [Fact]
    public void Der_Verlauf_spannt_sich_ueber_die_ganze_Breite()
    {
        var layout = Sparkline.Compute([10000, 20000, 15000], Breite, Hoehe);

        Assert.Equal(0, layout.Points[0].X, precision: 6);
        Assert.Equal(Breite, layout.Points[^1].X, precision: 6);
    }

    [Fact]
    public void Der_letzte_Punkt_steht_gesondert_zur_Verfuegung()
    {
        var layout = Sparkline.Compute([10000, 20000, 15000], Breite, Hoehe);

        Assert.Equal(layout.Points[^1], layout.Last);
    }

    /// <summary>
    /// Ein groesserer Betrag sitzt weiter OBEN, hat also ein kleineres Y.
    /// </summary>
    [Fact]
    public void Ein_hoeherer_Wert_liegt_weiter_oben()
    {
        var layout = Sparkline.Compute([10000, 90000], Breite, Hoehe);

        Assert.True(layout.Points[1].Y < layout.Points[0].Y);
    }

    /// <summary>
    /// Die Achse enthaelt immer die Null - ohne beschriftete Achse laesst
    /// sich sonst nicht erkennen, dass der Ausschlag winzig ist.
    /// </summary>
    [Fact]
    public void Die_Achse_enthaelt_immer_die_Null()
    {
        var layout = Sparkline.Compute([100000, 100500, 100200], Breite, Hoehe);

        Assert.True(layout.Scale.IncludesZero);
    }

    [Fact]
    public void Ohne_Flaeche_gibt_es_nichts_zu_zeichnen()
    {
        Assert.True(Sparkline.Compute([1000, 2000], 0, Hoehe).IsEmpty);
        Assert.True(Sparkline.Compute([1000, 2000], Breite, 0).IsEmpty);
    }
}
