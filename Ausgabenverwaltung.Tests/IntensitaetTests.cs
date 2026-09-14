using Ausgabenverwaltung.Core.Charts;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Einteilung von Betraegen in Stufen. Der springende Punkt ist die
/// Einteilung nach RANG: eine einzelne grosse Zahl darf die uebrigen
/// nicht alle auf dieselbe Stufe druecken.
/// </summary>
public class IntensitaetTests
{
    private static IReadOnlyDictionary<string, int> Stufen(
        params long[] werte)
    {
        var eingabe = werte
            .Select((wert, i) => (Schluessel: i.ToString(), Wert: wert))
            .ToDictionary(x => x.Schluessel, x => x.Wert);

        return Intensity.Steps(eingabe);
    }

    [Fact]
    public void Ohne_Werte_gibt_es_keine_Stufen()
    {
        Assert.Empty(Intensity.Steps(new Dictionary<string, long>()));
    }

    /// <summary>
    /// Der eigentliche Zweck: dreissig kleine Betraege neben einer
    /// Jahresmiete. Linear geteilt laege alles ausser der Miete auf
    /// Stufe 1, und die Einfaerbung saehe aus wie ein Fehler.
    /// </summary>
    [Fact]
    public void Ein_Ausreisser_drueckt_die_uebrigen_nicht_alle_auf_Stufe_eins()
    {
        var werte = new List<long>();
        for (var i = 1; i <= 30; i++)
        {
            werte.Add(i * 500);
        }

        werte.Add(1_200_000);

        var stufen = Stufen([.. werte]);

        // Es kommen tatsaechlich alle vier Stufen vor.
        Assert.Equal(4, stufen.Values.Distinct().Count());

        // Und die kleinen Betraege verteilen sich, statt zu verklumpen.
        var kleine = stufen.Take(30).Select(paar => paar.Value).Distinct().Count();
        Assert.True(kleine > 1, "Die kleinen Betraege liegen alle auf derselben Stufe.");
    }

    [Fact]
    public void Gleiche_Betraege_bekommen_dieselbe_Stufe()
    {
        var stufen = Stufen(1000, 5000, 5000, 5000, 9000, 20000, 50000, 80000);

        Assert.Equal(stufen["1"], stufen["2"]);
        Assert.Equal(stufen["2"], stufen["3"]);
    }

    /// <summary>
    /// Verglichen wird der Betrag. Ob eine Zelle im Plus oder im Minus
    /// steht, sagt bereits ihr Vorzeichen.
    /// </summary>
    [Fact]
    public void Das_Vorzeichen_aendert_die_Stufe_nicht()
    {
        var stufen = Stufen(-5000, 5000, 1000, 80000);

        Assert.Equal(stufen["0"], stufen["1"]);
    }

    [Fact]
    public void Der_groesste_Betrag_traegt_die_hoechste_Stufe()
    {
        var stufen = Stufen(1000, 2000, 3000, 900000);

        Assert.Equal(4, stufen["3"]);
    }

    [Fact]
    public void Der_kleinste_Betrag_traegt_die_niedrigste_Stufe()
    {
        var stufen = Stufen(1000, 2000, 3000, 900000);

        Assert.Equal(1, stufen["0"]);
    }

    /// <summary>
    /// Stufe 0 ist dem Aufrufer vorbehalten und heisst "hier steht gar
    /// kein Wert". Was hereingegeben wurde, hat immer einen.
    /// </summary>
    [Fact]
    public void Die_Stufe_null_kommt_im_Ergebnis_nie_vor()
    {
        var stufen = Stufen(0, 1000, 2000, 3000);

        Assert.All(stufen.Values, stufe => Assert.True(stufe >= 1));
    }

    [Fact]
    public void Lauter_gleiche_Betraege_ergeben_eine_einzige_Stufe()
    {
        var stufen = Stufen(5000, 5000, 5000, 5000);

        Assert.Single(stufen.Values.Distinct());
    }

    [Fact]
    public void Keine_Stufe_liegt_ueber_der_gewuenschten_Anzahl()
    {
        var eingabe = Enumerable.Range(1, 50)
            .ToDictionary(i => i, i => (long)(i * 137));

        var stufen = Intensity.Steps(eingabe, stepCount: 3);

        Assert.All(stufen.Values, stufe => Assert.InRange(stufe, 1, 3));
        Assert.Equal(3, stufen.Values.Distinct().Count());
    }

    [Fact]
    public void Eine_Stufenzahl_unter_eins_wird_angehoben()
    {
        var stufen = Intensity.Steps(
            new Dictionary<string, long> { ["a"] = 100, ["b"] = 900 },
            stepCount: 0);

        Assert.All(stufen.Values, stufe => Assert.Equal(1, stufe));
    }
}
