using Ausgabenverwaltung.Core.Display;
using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Chips unter der Filterleiste (<see cref="FilterChips"/>) und die
/// Schwelle, ab der sie einklappt (<see cref="Filterleiste"/>).
///
/// Beides gehoert zusammen: die Leiste darf nur deshalb verschwinden,
/// weil die Chips stehen bleiben. Ein Filter, der wirkt, ohne sich zu
/// zeigen, ist der haeufigste Grund fuer "meine Buchungen sind weg" -
/// wer die Chips also kaputtmacht, macht das Einklappen zu einem
/// Verstecken.
/// </summary>
public class FilterChipsTests
{
    private static IReadOnlyList<FilterChip> Chips(FilterZustand zustand)
        => FilterChips.Bestimme(zustand);

    [Fact]
    public void Ohne_gesetzten_Filter_gibt_es_keine_Chips()
        => Assert.Empty(Chips(new FilterZustand()));

    [Fact]
    public void Jeder_gesetzte_Filter_bekommt_seinen_Chip()
    {
        var chips = Chips(new FilterZustand
        {
            Zeitraum = "01.08.2026 – 31.08.2026",
            Kategorien = "Haushalt",
            Zahler = "2 Zahler",
            StatusOffen = true,
            NurEinnahmen = true,
            MeineKosten = true,
            Suche = "Rewe",
        });

        Assert.Equal(
            [
                FilterArt.Zeitraum, FilterArt.Kategorien, FilterArt.Zahler,
                FilterArt.Status, FilterArt.Buchungsart, FilterArt.MeineKosten,
                FilterArt.Suche,
            ],
            chips.Select(chip => chip.Art));
    }

    /// <summary>
    /// Beide Status-Haekchen zusammen schraenken nicht ein ("offen ODER
    /// beglichen" ist alles). Ein Chip dafuer waere eine Einschraenkung,
    /// die es nicht gibt - und ein Wegklicken, das nichts aendert.
    /// </summary>
    [Fact]
    public void Beide_Status_Haekchen_zusammen_ergeben_keinen_Chip()
        => Assert.Empty(Chips(new FilterZustand { StatusOffen = true, StatusBeglichen = true }));

    [Fact]
    public void Beide_Buchungsarten_zusammen_ergeben_keinen_Chip()
        => Assert.Empty(Chips(new FilterZustand { NurEinnahmen = true, NurAusgaben = true }));

    [Theory]
    [InlineData(true, false, "Nur offene")]
    [InlineData(false, true, "Nur beglichene")]
    public void Der_Status_Chip_sagt_welche_Haelfte_gilt(
        bool offen, bool beglichen, string erwartet)
    {
        var chip = Assert.Single(
            Chips(new FilterZustand { StatusOffen = offen, StatusBeglichen = beglichen }));

        Assert.Equal(erwartet, chip.Beschriftung);
    }

    /// <summary>
    /// Der Suchtext steht in Anfuehrungszeichen: ohne sie liesse sich ein
    /// Chip "Nur offene" nicht davon unterscheiden, dass jemand genau das
    /// in die Suche getippt hat.
    /// </summary>
    [Fact]
    public void Der_Suchtext_steht_in_Anfuehrungszeichen()
    {
        var chip = Assert.Single(Chips(new FilterZustand { Suche = "Nur offene" }));

        Assert.Equal("Suche: „Nur offene“", chip.Beschriftung);
        Assert.Equal(FilterArt.Suche, chip.Art);
    }

    [Fact]
    public void Ein_Suchtext_aus_Leerzeichen_zaehlt_nicht()
        => Assert.Empty(Chips(new FilterZustand { Suche = "   " }));

    /// <summary>
    /// Der Vorgabezeitraum ist kein Filter, sondern der Ausgangspunkt.
    /// Ihn zu bechippen hiesse, jede Ansicht dauerhaft als "gefiltert" zu
    /// bezeichnen - und der Knopf traege dann nie die Zahl null.
    /// </summary>
    [Fact]
    public void Der_Vorgabezeitraum_bekommt_keinen_Chip()
        => Assert.Empty(Chips(new FilterZustand { Zeitraum = null }));

    // ---------------- Die Schwelle ----------------

    [Fact]
    public void Ein_breites_Fenster_laesst_die_Leiste_aufgeklappt()
        => Assert.True(Filterleiste.PasstAufgeklappt(1200));

    [Fact]
    public void Ein_schmales_Fenster_klappt_sie_ein()
        => Assert.False(Filterleiste.PasstAufgeklappt(360));

    /// <summary>
    /// Vor der ersten Messung ist die Breite 0. Das gilt als "reicht" -
    /// eine Leiste, die beim Aufgehen des Fensters erst zuklappt und
    /// gleich wieder aufspringt, sieht nach einem Fehler aus.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void Eine_nicht_gemessene_Breite_klappt_nicht_ein(double breite)
        => Assert.True(Filterleiste.PasstAufgeklappt(breite));

    [Fact]
    public void Genau_auf_der_Schwelle_bleibt_sie_aufgeklappt()
        => Assert.True(
            Filterleiste.PasstAufgeklappt(Filterleiste.MindestbreiteAufgeklappt));
}
