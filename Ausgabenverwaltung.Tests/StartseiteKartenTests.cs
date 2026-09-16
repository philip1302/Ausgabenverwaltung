using Ausgabenverwaltung.Core.Formatting;
using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die beiden Rechnungen hinter den neuen Angaben der Startseite: die
/// Aufstellung "wofuer diesen Monat" und die Einordnung einer Monatszahl.
///
/// Beide stehen in Core und kommen ohne Datenbank und ohne Fenster aus
/// (Regel 7) - hier wird also genau das geprueft, was der Anwender
/// nachher liest.
/// </summary>
public class StartseiteKartenTests
{
    // ================= Wofuer diesen Monat =================

    private static CategorySum Summe(int id, string name, long cents)
        => new(id, name, cents);

    [Fact]
    public void Die_groesste_Kategorie_steht_oben()
    {
        var zeilen = CategoryShares.Top(
            [Summe(1, "Essen", 5000), Summe(2, "Wohnen", 90000), Summe(3, "Auto", 20000)],
            count: 5);

        Assert.Equal(["Wohnen", "Auto", "Essen"], zeilen.Select(z => z.Name));
    }

    [Fact]
    public void Der_Anteil_ist_der_Teil_am_Ganzen()
    {
        var zeilen = CategoryShares.Top(
            [Summe(1, "Wohnen", 75000), Summe(2, "Essen", 25000)], count: 5);

        Assert.Equal(0.75, zeilen[0].Share, precision: 6);
        Assert.Equal(0.25, zeilen[1].Share, precision: 6);
    }

    /// <summary>
    /// Der Rest wird zusammengefasst, damit die Karte nicht mit dem
    /// Kategorienbaum um die Wette waechst.
    /// </summary>
    [Fact]
    public void Was_nicht_mehr_hineinpasst_wird_zusammengefasst()
    {
        var summen = Enumerable.Range(1, 8)
            .Select(i => Summe(i, $"K{i}", 1000L * i))
            .ToList();

        var zeilen = CategoryShares.Top(summen, count: 3);

        Assert.Equal(4, zeilen.Count);

        var rest = zeilen[^1];
        Assert.Null(rest.CategoryId);
        Assert.Equal("Übrige (5)", rest.Name);
        Assert.Equal(1000 + 2000 + 3000 + 4000 + 5000, rest.SumCents);
    }

    /// <summary>
    /// Bliebe genau eine Kategorie uebrig, wird sie beim Namen genannt:
    /// "Übrige (1)" verschweigt ihn, ohne dafuer Platz zu sparen.
    /// </summary>
    [Fact]
    public void Eine_einzelne_uebrige_Kategorie_wird_beim_Namen_genannt()
    {
        var summen = Enumerable.Range(1, 4)
            .Select(i => Summe(i, $"K{i}", 1000L * i))
            .ToList();

        var zeilen = CategoryShares.Top(summen, count: 3);

        Assert.Equal(4, zeilen.Count);
        Assert.All(zeilen, zeile => Assert.NotNull(zeile.CategoryId));
    }

    [Fact]
    public void Eine_Kategorie_ohne_Betrag_taucht_nicht_auf()
    {
        var zeilen = CategoryShares.Top(
            [Summe(1, "Wohnen", 5000), Summe(2, "Urlaub", 0)], count: 5);

        Assert.Single(zeilen);
        Assert.Equal("Wohnen", zeilen[0].Name);
    }

    [Fact]
    public void Ohne_Buchungen_bleibt_die_Aufstellung_leer()
        => Assert.Empty(CategoryShares.Top([], count: 5));

    [Fact]
    public void Gleiche_Betraege_stehen_in_stabiler_Reihenfolge()
    {
        var zeilen = CategoryShares.Top(
            [Summe(1, "Wohnen", 5000), Summe(2, "Auto", 5000)], count: 5);

        Assert.Equal(["Auto", "Wohnen"], zeilen.Select(z => z.Name));
    }

    // ================= Einordnung der Monatszahl =================

    [Fact]
    public void Ueber_dem_Schnitt_steht_um_wieviel()
    {
        // Schnitt 1.000 €, dieser Monat 1.200 € - also 20 % darueber.
        var einordnung = MonthComparison.Describe(
            120000, [100000, 100000, 100000], monatLaeuft: false);

        Assert.NotNull(einordnung);
        Assert.Equal("20 % über dem Schnitt der letzten 3 Monate", einordnung.Text);
    }

    [Fact]
    public void Unter_dem_Schnitt_steht_ebenso()
    {
        var einordnung = MonthComparison.Describe(
            50000, [100000, 100000], monatLaeuft: false);

        Assert.NotNull(einordnung);
        Assert.Equal("50 % unter dem Schnitt der letzten 2 Monate", einordnung.Text);
    }

    /// <summary>
    /// Ein paar Prozent sind kein Befund, sondern ein Einkauf, der auf die
    /// andere Seite des Monatsersten gefallen ist.
    /// </summary>
    [Fact]
    public void Eine_kleine_Abweichung_heisst_wie_immer()
    {
        var einordnung = MonthComparison.Describe(
            102000, [100000, 100000], monatLaeuft: false);

        Assert.NotNull(einordnung);
        Assert.Equal("wie im Schnitt der letzten 2 Monate", einordnung.Text);
    }

    /// <summary>
    /// Der wichtigste Zusatz: solange der Monat laeuft, ist es ein
    /// Vergleich BIS HEUTE und keiner ganzer Monate.
    /// </summary>
    [Fact]
    public void Im_laufenden_Monat_sagt_der_Satz_bis_heute()
    {
        var einordnung = MonthComparison.Describe(
            120000, [100000, 100000], monatLaeuft: true);

        Assert.NotNull(einordnung);
        Assert.StartsWith("Bis heute ", einordnung.Text);
        Assert.Contains("bis zum selben Tag gezählt", einordnung.Hinweis);
    }

    [Fact]
    public void Der_Hinweis_nennt_den_Schnitt_als_Betrag()
    {
        var einordnung = MonthComparison.Describe(
            120000, [100000, 140000], monatLaeuft: false);

        Assert.NotNull(einordnung);
        Assert.Equal(
            $"Schnitt der letzten 2 Monate: {EuroText.Format(120000)}",
            einordnung.Hinweis);
    }

    [Fact]
    public void Ein_einzelner_Vormonat_ordnet_nichts_ein()
        => Assert.Null(MonthComparison.Describe(120000, [100000], monatLaeuft: false));

    /// <summary>
    /// Monate vor der ersten Buchung sind keine Nullmonate, sondern gar
    /// keine Monate - sonst waere jeder Anfaenger dauerhaft "weit ueber
    /// dem Schnitt".
    /// </summary>
    [Fact]
    public void Monate_vor_der_ersten_Buchung_zaehlen_nicht_mit()
    {
        var einordnung = MonthComparison.Describe(
            120000, [0, 0, 0, 100000, 100000], monatLaeuft: false);

        Assert.NotNull(einordnung);
        Assert.Contains("letzten 2 Monate", einordnung.Text);
    }

    /// <summary>
    /// Eine Null MITTEN in der Reihe ist dagegen ein echter Monat ohne
    /// Buchung und gehoert in den Schnitt.
    /// </summary>
    [Fact]
    public void Ein_leerer_Monat_zwischendrin_zaehlt_mit()
    {
        var einordnung = MonthComparison.Describe(
            120000, [100000, 0, 200000], monatLaeuft: false);

        Assert.NotNull(einordnung);
        Assert.Contains("letzten 3 Monate", einordnung.Text);
        Assert.Contains(EuroText.Format(100000), einordnung.Hinweis);
    }

    /// <summary>
    /// Steht die Kachel auf null, sagt ihre Zusatzzeile das bereits im
    /// Klartext - "100 % unter dem Schnitt" waere dieselbe Nachricht ein
    /// zweites Mal.
    /// </summary>
    [Fact]
    public void Ein_Monat_ganz_ohne_Buchung_bekommt_keine_Einordnung()
        => Assert.Null(MonthComparison.Describe(0, [100000, 100000], monatLaeuft: true));

    [Fact]
    public void Ohne_Vormonate_mit_Betrag_bleibt_die_Zeile_weg()
        => Assert.Null(MonthComparison.Describe(120000, [0, 0, 0], monatLaeuft: true));
}
