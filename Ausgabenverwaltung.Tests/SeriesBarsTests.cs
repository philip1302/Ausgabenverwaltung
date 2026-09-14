using Ausgabenverwaltung.Core.Charts;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Der Zwei-Jahres-Vergleich. Der Vorjahresbalken steht breit im
/// Hintergrund, der des laufenden Jahres schmaler davor - beide auf
/// derselben Achse und um dieselbe Mitte.
/// </summary>
public class SeriesBarsTests
{
    private const double Breite = 600;
    private const double Hoehe = 200;

    private static ComparePeriod Monat(
        string key = "2026-03", long vorjahr = 0, long jahr = 0)
        => new(key, "Mär", vorjahr, jahr);

    [Fact]
    public void Ohne_Abschnitte_gibt_es_nichts_zu_zeichnen()
    {
        Assert.True(SeriesBars.Compare([], Breite, Hoehe).IsEmpty);
    }

    [Fact]
    public void Ohne_Flaeche_gibt_es_nichts_zu_zeichnen()
    {
        var werte = new[] { Monat(vorjahr: 1000, jahr: 2000) };

        Assert.True(SeriesBars.Compare(werte, 0, Hoehe).IsEmpty);
        Assert.True(SeriesBars.Compare(werte, Breite, 0).IsEmpty);
    }

    [Fact]
    public void Jeder_Abschnitt_bekommt_beide_Balken()
    {
        var layout = SeriesBars.Compare(
            [Monat(vorjahr: 50000, jahr: 80000)], Breite, Hoehe);

        Assert.Contains(layout.Bars, b => b.IstVorjahr);
        Assert.Contains(layout.Bars, b => !b.IstVorjahr);
    }

    /// <summary>
    /// Der hintere Balken muss breiter sein, sonst verdeckt der vordere
    /// ihn vollstaendig und der Bezug ist weg.
    /// </summary>
    [Fact]
    public void Der_Vorjahresbalken_ist_breiter_als_der_des_laufenden_Jahres()
    {
        var layout = SeriesBars.Compare(
            [Monat(vorjahr: 50000, jahr: 80000)], Breite, Hoehe);

        var vorjahr = layout.Bars.Single(b => b.IstVorjahr);
        var jahr = layout.Bars.Single(b => !b.IstVorjahr);

        Assert.True(jahr.Width < vorjahr.Width);
    }

    /// <summary>
    /// Ueberlagert heisst: beide teilen sich eine Mitte. Sonst laesen sich
    /// die beiden Hoehen nicht an einer Kante vergleichen.
    /// </summary>
    [Fact]
    public void Beide_Balken_stehen_um_dieselbe_Mitte()
    {
        var layout = SeriesBars.Compare(
            [Monat(vorjahr: 50000, jahr: 80000)], Breite, Hoehe);

        var vorjahr = layout.Bars.Single(b => b.IstVorjahr);
        var jahr = layout.Bars.Single(b => !b.IstVorjahr);

        Assert.Equal(
            vorjahr.X + vorjahr.Width / 2,
            jahr.X + jahr.Width / 2,
            precision: 6);
    }

    /// <summary>
    /// Der Vorjahresbalken kommt zuerst in die Liste, weil die Ansicht in
    /// dieser Reihenfolge zeichnet - sonst laege der Bezug vor dem
    /// Gegenstand.
    /// </summary>
    [Fact]
    public void Das_Vorjahr_wird_zuerst_gezeichnet()
    {
        var layout = SeriesBars.Compare(
            [Monat(vorjahr: 50000, jahr: 80000)], Breite, Hoehe);

        Assert.True(layout.Bars[0].IstVorjahr);
        Assert.False(layout.Bars[1].IstVorjahr);
    }

    /// <summary>
    /// Der Vergleich lebt davon, dass beide Reihen denselben Massstab
    /// haben: der doppelte Betrag ist auch doppelt so hoch.
    /// </summary>
    [Fact]
    public void Beide_Reihen_teilen_sich_eine_Achse()
    {
        var layout = SeriesBars.Compare(
            [Monat(vorjahr: 40000, jahr: 80000)], Breite, Hoehe);

        var vorjahr = layout.Bars.Single(b => b.IstVorjahr);
        var jahr = layout.Bars.Single(b => !b.IstVorjahr);

        Assert.Equal(vorjahr.Height * 2, jahr.Height, precision: 6);
    }

    [Fact]
    public void Ein_Abschnitt_ohne_Buchungen_erzeugt_keinen_Balken()
    {
        var layout = SeriesBars.Compare(
            [Monat("2026-01", vorjahr: 50000, jahr: 80000), Monat("2026-02")],
            Breite,
            Hoehe);

        Assert.DoesNotContain(layout.Bars, b => b.Key == "2026-02");
    }

    /// <summary>
    /// Ein leerer Monat behaelt trotzdem seinen Platz auf der Zeitachse -
    /// sonst ruecken die uebrigen zusammen und der Jahresverlauf stimmt
    /// nicht mehr.
    /// </summary>
    [Fact]
    public void Ein_leerer_Abschnitt_behaelt_seinen_Platz()
    {
        var layout = SeriesBars.Compare(
            [
                Monat("2026-01", vorjahr: 50000, jahr: 50000),
                Monat("2026-02"),
                Monat("2026-03", vorjahr: 50000, jahr: 50000),
            ],
            Breite,
            Hoehe);

        var erster = layout.Bars.First(b => b.Key == "2026-01");
        var dritter = layout.Bars.First(b => b.Key == "2026-03");

        // Zwei Abschnitte Abstand bei drei Abschnitten auf der Breite.
        Assert.Equal(Breite / 3 * 2, dritter.X - erster.X, precision: 6);
    }

    [Fact]
    public void Ein_hoeherer_Betrag_beginnt_weiter_oben()
    {
        var layout = SeriesBars.Compare(
            [Monat(vorjahr: 20000, jahr: 90000)], Breite, Hoehe);

        var vorjahr = layout.Bars.Single(b => b.IstVorjahr);
        var jahr = layout.Bars.Single(b => !b.IstVorjahr);

        Assert.True(jahr.Y < vorjahr.Y);
    }

    [Fact]
    public void Das_Gitternetz_und_die_Achsenbeschriftung_stehen_bereit()
    {
        var layout = SeriesBars.Compare(
            [Monat(vorjahr: 20000, jahr: 90000)], Breite, Hoehe);

        Assert.NotEmpty(layout.Lines);
        Assert.NotEmpty(layout.Ticks);
    }
}
