using Ausgabenverwaltung.Core.Charts;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Umrechnung von Betraegen in Bildpunkte. Sie steht in Core, damit
/// genau das hier moeglich ist: pruefen, ob ein Balken an der richtigen
/// Stelle sitzt, ohne ein Fenster zu oeffnen (Regel 7).
///
/// Der Ursprung liegt wie bei Bildschirmkoordinaten OBEN links - ein
/// groesseres Y heisst weiter unten. Das dreht die Erwartungen um und ist
/// die haeufigste Fehlerquelle bei so einer Rechnung.
/// </summary>
public class BarChartTests
{
    private const double Breite = 600;
    private const double Hoehe = 200;

    private static PeriodValue Wert(
        string key = "2026-03",
        long eigene = 0,
        long fremdeOffene = 0,
        long einnahmen = 0)
        => new(key, "Mär 2026", eigene, fremdeOffene, einnahmen);

    // ================= Detailansicht =================

    [Fact]
    public void Die_Detailansicht_zeichnet_je_Abschnitt_beide_Saeulen()
    {
        var layout = BarChart.Detailed(
            new[] { Wert(eigene: 50000, einnahmen: 80000) }, Breite, Hoehe);

        Assert.Contains(layout.Bars, b => b.Kind == BarKind.OwnExpenses);
        Assert.Contains(layout.Bars, b => b.Kind == BarKind.Income);
    }

    /// <summary>
    /// Gestapelt heisst: das Offene sitzt unmittelbar auf den eigenen
    /// Ausgaben, ohne Luecke und ohne Ueberlappung.
    /// </summary>
    [Fact]
    public void Die_offenen_Fremdausgaben_sitzen_ohne_Luecke_auf_den_eigenen()
    {
        var layout = BarChart.Detailed(
            new[] { Wert(eigene: 50000, fremdeOffene: 30000) }, Breite, Hoehe);

        var eigene = layout.Bars.Single(b => b.Kind == BarKind.OwnExpenses);
        var offene = layout.Bars.Single(b => b.Kind == BarKind.ForeignOpenExpenses);

        // Die Unterkante des oberen Teils ist die Oberkante des unteren.
        Assert.Equal(eigene.Y, offene.Y + offene.Height, precision: 6);
    }

    [Fact]
    public void Die_beiden_Saeulen_ueberlappen_einander_nicht()
    {
        var layout = BarChart.Detailed(
            new[] { Wert(eigene: 50000, einnahmen: 80000) }, Breite, Hoehe);

        var ausgaben = layout.Bars.Single(b => b.Kind == BarKind.OwnExpenses);
        var einnahmen = layout.Bars.Single(b => b.Kind == BarKind.Income);

        Assert.True(ausgaben.X + ausgaben.Width <= einnahmen.X);
    }

    [Fact]
    public void Beide_Saeulen_stehen_auf_derselben_Grundlinie()
    {
        var layout = BarChart.Detailed(
            new[] { Wert(eigene: 50000, einnahmen: 80000) }, Breite, Hoehe);

        var ausgaben = layout.Bars.Single(b => b.Kind == BarKind.OwnExpenses);
        var einnahmen = layout.Bars.Single(b => b.Kind == BarKind.Income);

        Assert.Equal(ausgaben.Y + ausgaben.Height, einnahmen.Y + einnahmen.Height, precision: 6);
    }

    /// <summary>
    /// Nur wenn beide Saeulen dieselbe Achse benutzen, laesst sich
    /// ueberhaupt vergleichen - und Vergleichen ist der Zweck dieser
    /// Ansicht.
    /// </summary>
    [Fact]
    public void Der_hoehere_Betrag_ergibt_den_hoeheren_Balken()
    {
        var layout = BarChart.Detailed(
            new[] { Wert(eigene: 40000, einnahmen: 120000) }, Breite, Hoehe);

        var ausgaben = layout.Bars.Single(b => b.Kind == BarKind.OwnExpenses);
        var einnahmen = layout.Bars.Single(b => b.Kind == BarKind.Income);

        Assert.True(einnahmen.Height > ausgaben.Height);

        // Dreifacher Betrag, also ungefaehr dreifache Hoehe.
        Assert.Equal(3.0, einnahmen.Height / ausgaben.Height, precision: 6);
    }

    [Fact]
    public void Ein_Betrag_von_null_zeichnet_keinen_Balken()
    {
        var layout = BarChart.Detailed(
            new[] { Wert(eigene: 50000, fremdeOffene: 0, einnahmen: 0) }, Breite, Hoehe);

        Assert.DoesNotContain(layout.Bars, b => b.Kind == BarKind.ForeignOpenExpenses);
        Assert.DoesNotContain(layout.Bars, b => b.Kind == BarKind.Income);
    }

    [Fact]
    public void Mehrere_Abschnitte_stehen_nebeneinander_ohne_Ueberlappung()
    {
        var layout = BarChart.Detailed(
            new[]
            {
                Wert("2026-01", eigene: 10000),
                Wert("2026-02", eigene: 20000),
                Wert("2026-03", eigene: 30000),
            },
            Breite, Hoehe);

        var balken = layout.Bars
            .Where(b => b.Kind == BarKind.OwnExpenses)
            .OrderBy(b => b.X)
            .ToList();

        Assert.Equal(3, balken.Count);
        Assert.True(balken[0].X + balken[0].Width <= balken[1].X);
        Assert.True(balken[1].X + balken[1].Width <= balken[2].X);
    }

    [Fact]
    public void Alle_Balken_bleiben_innerhalb_der_Zeichenflaeche()
    {
        var layout = BarChart.Detailed(
            new[]
            {
                Wert("2026-01", eigene: 250000, fremdeOffene: 40000, einnahmen: 310000),
                Wert("2026-02", eigene: 10000, einnahmen: 5000),
            },
            Breite, Hoehe);

        Assert.All(layout.Bars, b =>
        {
            Assert.InRange(b.X, 0, Breite);
            Assert.InRange(b.X + b.Width, 0, Breite);
            Assert.InRange(b.Y, -0.001, Hoehe);
            Assert.InRange(b.Y + b.Height, -0.001, Hoehe + 0.001);
        });
    }

    [Fact]
    public void Die_Detailansicht_zeigt_zwei_Durchschnittslinien()
    {
        var layout = BarChart.Detailed(
            new[]
            {
                Wert("2026-01", eigene: 40000, einnahmen: 100000),
                Wert("2026-02", eigene: 60000, einnahmen: 200000),
            },
            Breite, Hoehe);

        Assert.Single(layout.Lines, l => l.Kind == ChartLineKind.AverageExpenses);
        Assert.Single(layout.Lines, l => l.Kind == ChartLineKind.AverageIncome);
    }

    [Fact]
    public void Die_Durchschnittslinie_liegt_auf_dem_Mittelwert()
    {
        var layout = BarChart.Detailed(
            new[]
            {
                Wert("2026-01", eigene: 40000),
                Wert("2026-02", eigene: 60000),
            },
            Breite, Hoehe);

        var linie = layout.Lines.Single(l => l.Kind == ChartLineKind.AverageExpenses);

        Assert.Equal(50000, linie.ValueCents);
    }

    /// <summary>
    /// Eine Durchschnittslinie bei null saehe wie die Nulllinie aus und
    /// verwirrte mehr, als sie hilft.
    /// </summary>
    [Fact]
    public void Ein_Durchschnitt_von_null_wird_nicht_gezeichnet()
    {
        var layout = BarChart.Detailed(new[] { Wert(eigene: 50000) }, Breite, Hoehe);

        Assert.DoesNotContain(layout.Lines, l => l.Kind == ChartLineKind.AverageIncome);
    }

    // ================= Nettoansicht =================

    [Fact]
    public void Ein_positives_Netto_haengt_ueber_der_Nulllinie()
    {
        var layout = BarChart.Net(
            new[] { Wert(eigene: 20000, einnahmen: 100000) }, Breite, Hoehe);

        var balken = layout.Bars.Single();
        var nullLinie = layout.Lines.Single(l => l.Kind == ChartLineKind.Zero);

        Assert.Equal(BarKind.NetPositive, balken.Kind);
        Assert.Equal(80000, balken.ValueCents);

        // Oberkante ueber der Null, Unterkante auf der Null.
        Assert.True(balken.Y < nullLinie.Y);
        Assert.Equal(nullLinie.Y, balken.Y + balken.Height, precision: 6);
    }

    [Fact]
    public void Ein_negatives_Netto_haengt_unter_der_Nulllinie()
    {
        var layout = BarChart.Net(
            new[] { Wert(eigene: 120000, einnahmen: 20000) }, Breite, Hoehe);

        var balken = layout.Bars.Single();
        var nullLinie = layout.Lines.Single(l => l.Kind == ChartLineKind.Zero);

        Assert.Equal(BarKind.NetNegative, balken.Kind);
        Assert.Equal(-100000, balken.ValueCents);

        Assert.Equal(nullLinie.Y, balken.Y, precision: 6);
        Assert.True(balken.Y + balken.Height > nullLinie.Y);
    }

    [Fact]
    public void Offene_Fremdausgaben_zaehlen_beim_Netto_als_getragen()
    {
        var layout = BarChart.Net(
            new[] { Wert(eigene: 30000, fremdeOffene: 20000, einnahmen: 100000) },
            Breite, Hoehe);

        // 100.000 - (30.000 + 20.000)
        Assert.Equal(50000, layout.Bars.Single().ValueCents);
    }

    [Fact]
    public void Ein_Netto_von_null_zeichnet_keinen_Balken()
    {
        var layout = BarChart.Net(
            new[] { Wert(eigene: 50000, einnahmen: 50000) }, Breite, Hoehe);

        Assert.Empty(layout.Bars);
    }

    [Fact]
    public void Die_Nettoansicht_zeigt_genau_eine_Durchschnittslinie()
    {
        var layout = BarChart.Net(
            new[]
            {
                Wert("2026-01", eigene: 20000, einnahmen: 100000),
                Wert("2026-02", eigene: 60000, einnahmen: 100000),
            },
            Breite, Hoehe);

        var durchschnitte = layout.Lines
            .Where(l => l.Kind is ChartLineKind.AverageNet
                          or ChartLineKind.AverageExpenses
                          or ChartLineKind.AverageIncome)
            .ToList();

        Assert.Single(durchschnitte);
        Assert.Equal(60000, durchschnitte[0].ValueCents);
    }

    // ================= Achse und Randfaelle =================

    [Fact]
    public void Zu_jedem_Achsenstrich_gehoert_eine_Gitterlinie()
    {
        var layout = BarChart.Detailed(
            new[] { Wert(eigene: 50000, einnahmen: 80000) }, Breite, Hoehe);

        var gitter = layout.Lines
            .Where(l => l.Kind is ChartLineKind.Grid or ChartLineKind.Zero)
            .ToList();

        Assert.Equal(layout.Ticks.Count, gitter.Count);
    }

    [Fact]
    public void Die_Nulllinie_hebt_sich_vom_uebrigen_Gitternetz_ab()
    {
        var layout = BarChart.Net(
            new[]
            {
                Wert("2026-01", eigene: 20000, einnahmen: 100000),
                Wert("2026-02", eigene: 100000, einnahmen: 20000),
            },
            Breite, Hoehe);

        Assert.Single(layout.Lines, l => l.Kind == ChartLineKind.Zero);
    }

    [Fact]
    public void Ohne_Abschnitte_entsteht_ein_leeres_Diagramm_statt_einer_Ausnahme()
    {
        var detail = BarChart.Detailed(Array.Empty<PeriodValue>(), Breite, Hoehe);
        var netto = BarChart.Net(Array.Empty<PeriodValue>(), Breite, Hoehe);

        Assert.True(detail.IsEmpty);
        Assert.True(netto.IsEmpty);
    }

    /// <summary>
    /// Beim ersten Aufbau des Fensters ist die Zeichenflaeche noch nicht
    /// vermessen - dann kommt hier eine Null oder ein negativer Wert an.
    /// </summary>
    [Theory]
    [InlineData(0, 200)]
    [InlineData(600, 0)]
    [InlineData(-10, -10)]
    public void Eine_noch_nicht_vermessene_Flaeche_wirft_nicht(double breite, double hoehe)
    {
        var layout = BarChart.Detailed(new[] { Wert(eigene: 50000) }, breite, hoehe);

        Assert.True(layout.IsEmpty);
    }

    [Fact]
    public void Ein_Abschnitt_ganz_ohne_Buchungen_bleibt_als_Luecke_stehen()
    {
        var layout = BarChart.Detailed(
            new[]
            {
                Wert("2026-01", eigene: 50000),
                Wert("2026-02"),
                Wert("2026-03", eigene: 30000),
            },
            Breite, Hoehe);

        // Der leere Februar zeichnet nichts, verschiebt aber den Maerz
        // nicht nach links - sonst waere die Zeitachse falsch.
        var maerz = layout.Bars.Single(b => b.Key == "2026-03");
        Assert.True(maerz.X > Breite / 2);
    }
}
