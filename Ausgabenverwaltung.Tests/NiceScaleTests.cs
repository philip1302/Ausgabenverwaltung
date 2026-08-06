using Ausgabenverwaltung.Core.Charts;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Achseneinteilung entscheidet, ob sich aus einem Diagramm etwas
/// ablesen laesst. Krumme Beschriftungen (1.337 €, 2.674 €) sind
/// rechnerisch richtig und praktisch wertlos.
/// </summary>
public class NiceScaleTests
{
    [Fact]
    public void Die_Schrittweite_ist_eine_runde_Zahl()
    {
        // 0 bis 4.200 EUR
        var scale = NiceScale.Compute(new long[] { 0, 420000 });

        // 1, 2 oder 5 mal eine Zehnerpotenz - nichts dazwischen.
        var ziffernfolge = scale.StepCents;
        while (ziffernfolge % 10 == 0 && ziffernfolge > 10)
        {
            ziffernfolge /= 10;
        }

        Assert.Contains(ziffernfolge, new long[] { 1, 2, 5, 10 });
    }

    [Fact]
    public void Alle_Striche_liegen_auf_einem_Vielfachen_der_Schrittweite()
    {
        var scale = NiceScale.Compute(new long[] { -33300, 128800, 71200 });

        Assert.All(scale.Ticks, tick => Assert.Equal(0, tick % scale.StepCents));
    }

    /// <summary>
    /// Der haeufigste Weg, mit einem korrekten Diagramm etwas Falsches zu
    /// behaupten: die Achse nicht bei null beginnen lassen.
    /// </summary>
    [Fact]
    public void Die_Null_liegt_immer_auf_der_Achse()
    {
        var nurHoch = NiceScale.Compute(new long[] { 500000, 520000, 510000 });
        Assert.True(nurHoch.IncludesZero);
        Assert.Equal(0, nurHoch.MinCents);

        var nurTief = NiceScale.Compute(new long[] { -500000, -520000 });
        Assert.True(nurTief.IncludesZero);
        Assert.Equal(0, nurTief.MaxCents);
    }

    [Fact]
    public void Ein_Bereich_ueber_die_Null_hinweg_umfasst_beide_Seiten()
    {
        var scale = NiceScale.Compute(new long[] { -80000, 120000 });

        Assert.True(scale.MinCents <= -80000);
        Assert.True(scale.MaxCents >= 120000);
        Assert.Contains(0L, scale.Ticks);
    }

    [Fact]
    public void Der_hoechste_Wert_beruehrt_nie_die_obere_Kante()
    {
        // 100 EUR trifft eine runde Grenze genau - der Balken saehe sonst
        // abgeschnitten aus.
        var scale = NiceScale.Compute(new long[] { 10000 });

        Assert.True(scale.MaxCents > 10000);
    }

    [Fact]
    public void Der_tiefste_Wert_beruehrt_nie_die_untere_Kante()
    {
        var scale = NiceScale.Compute(new long[] { -10000 });

        Assert.True(scale.MinCents < -10000);
    }

    /// <summary>
    /// Ohne Ersatzachse waere die Spanne null - und jede Umrechnung von
    /// Wert auf Bildpunkt eine Division durch null.
    /// </summary>
    [Fact]
    public void Ohne_Werte_entsteht_trotzdem_eine_brauchbare_Achse()
    {
        var scale = NiceScale.Compute(Array.Empty<long>());

        Assert.True(scale.SpanCents > 0);
        Assert.NotEmpty(scale.Ticks);
    }

    [Fact]
    public void Lauter_Nullwerte_ergeben_ebenfalls_eine_brauchbare_Achse()
    {
        var scale = NiceScale.Compute(new long[] { 0, 0, 0 });

        Assert.True(scale.SpanCents > 0);
        Assert.True(scale.IncludesZero);
    }

    [Fact]
    public void Ein_einzelner_Wert_ergibt_eine_Achse_mit_Spanne()
    {
        var scale = NiceScale.Compute(new long[] { 250000 });

        Assert.True(scale.SpanCents > 0);
        Assert.True(scale.MaxCents >= 250000);
    }

    [Fact]
    public void Die_Striche_laufen_von_der_Unter_bis_zur_Obergrenze()
    {
        var scale = NiceScale.Compute(new long[] { -45000, 90000 });

        Assert.Equal(scale.MinCents, scale.Ticks[0]);
        Assert.Equal(scale.MaxCents, scale.Ticks[^1]);
    }

    [Fact]
    public void Die_Striche_steigen_lueckenlos_um_die_Schrittweite()
    {
        var scale = NiceScale.Compute(new long[] { 0, 777700 });

        for (var i = 1; i < scale.Ticks.Count; i++)
        {
            Assert.Equal(scale.StepCents, scale.Ticks[i] - scale.Ticks[i - 1]);
        }
    }

    /// <summary>
    /// Nicht sklavisch, aber in der Naehe - sonst wird die Achse
    /// entweder leer oder unlesbar dicht.
    /// </summary>
    [Theory]
    [InlineData(1000L)]
    [InlineData(150000L)]
    [InlineData(9999999L)]
    public void Die_Anzahl_der_Striche_bleibt_im_lesbaren_Rahmen(long groesster)
    {
        var scale = NiceScale.Compute(new[] { groesster });

        Assert.InRange(scale.Ticks.Count, 2, 12);
    }

    [Fact]
    public void Sehr_kleine_Betraege_ergeben_keine_Schrittweite_von_null()
    {
        var scale = NiceScale.Compute(new long[] { 1, 2 });

        Assert.True(scale.StepCents >= 1);
        Assert.True(scale.SpanCents > 0);
    }
}
