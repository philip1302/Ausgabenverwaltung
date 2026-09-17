using Ausgabenverwaltung.Core.Errors;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Dass hoechstens EIN Band zugleich sichtbar ist und dabei nichts
/// verlorengeht.
/// </summary>
public class BandauswahlTests
{
    private static Bandmeldung Band(Bandrang rang, string titel)
        => new(rang, titel, "Eine Kurzzeile.", "Der lange Text dazu.");

    [Fact]
    public void Ohne_Kandidaten_bleibt_die_Bandzone_leer()
    {
        var lage = Bandauswahl.Waehle([]);

        Assert.False(lage.Sichtbar);
        Assert.Equal(0, lage.Gesamt);
        Assert.Null(lage.Zaehltext);
    }

    [Fact]
    public void Bei_genau_einem_Band_gibt_es_keinen_Zaehler()
    {
        var lage = Bandauswahl.Waehle([Band(Bandrang.Hinweis, "A")]);

        Assert.True(lage.Sichtbar);
        Assert.Equal(0, lage.Index);
        Assert.Equal(1, lage.Nummer);
        Assert.Equal(1, lage.Gesamt);

        // "1 von 1" waere reine Ziffernkosmetik.
        Assert.Null(lage.Zaehltext);
        Assert.False(lage.MehrereVorhanden);
    }

    [Fact]
    public void Der_dringendste_Rang_kommt_zuerst_unabhaengig_von_der_Reihenfolge()
    {
        Bandmeldung[] kandidaten =
        [
            Band(Bandrang.Hinweis, "Hinweis"),
            Band(Bandrang.Fehler, "Fehler"),
            Band(Bandrang.Warnung, "Warnung"),
        ];

        var lage = Bandauswahl.Waehle(kandidaten);

        Assert.Equal("Fehler", kandidaten[lage.Index].Titel);
        Assert.Equal("3", lage.Zaehltext![^1..]);
    }

    // Der Index zeigt in die UEBERGEBENE Liste, nicht in die sortierte -
    // sonst griffe der Aufrufer die Knoepfe eines anderen Bandes ab.
    [Fact]
    public void Der_Index_zeigt_in_die_uebergebene_Liste()
    {
        Bandmeldung[] kandidaten =
        [
            Band(Bandrang.Hinweis, "Hinweis"),
            Band(Bandrang.Erfolg, "Erfolg"),
            Band(Bandrang.Fehler, "Fehler"),
        ];

        Assert.Equal(2, Bandauswahl.Waehle(kandidaten).Index);
    }

    [Fact]
    public void Bei_gleichem_Rang_bleibt_das_aeltere_Band_vorn()
    {
        Bandmeldung[] kandidaten =
        [
            Band(Bandrang.Hinweis, "zuerst"),
            Band(Bandrang.Hinweis, "danach"),
        ];

        var lage = Bandauswahl.Waehle(kandidaten);

        Assert.Equal("zuerst", kandidaten[lage.Index].Titel);
    }

    [Fact]
    public void Weiterblaettern_zeigt_das_naechstdringende_Band()
    {
        Bandmeldung[] kandidaten =
        [
            Band(Bandrang.Hinweis, "Hinweis"),
            Band(Bandrang.Fehler, "Fehler"),
        ];

        var zweites = Bandauswahl.Waehle(kandidaten, wunschNummer: 2);

        Assert.Equal("Hinweis", kandidaten[zweites.Index].Titel);
        Assert.Equal("2 von 2", zweites.Zaehltext);
    }

    // Faellt ein Band weg, waehrend der Anwender auf Nummer 3 steht, darf
    // der Zaehler nicht ins Leere laufen - dann bliebe die Bandzone leer,
    // obwohl noch etwas ansteht.
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(2, 2)]
    [InlineData(9, 2)]
    public void Eine_Wunschnummer_ausserhalb_des_Bereichs_wird_zurechtgerueckt(
        int wunsch, int erwartet)
    {
        Bandmeldung[] kandidaten =
        [
            Band(Bandrang.Fehler, "Fehler"),
            Band(Bandrang.Hinweis, "Hinweis"),
        ];

        Assert.Equal(erwartet, Bandauswahl.Waehle(kandidaten, wunsch).Nummer);
    }
}
