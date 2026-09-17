using Ausgabenverwaltung.Core.Errors;
using Ausgabenverwaltung.ViewModels;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Das Verhalten der einen Bandzone: was sie zeigt, was "Schließen"
/// schliesst und was beim Durchblaettern passiert.
///
/// Die Auswahl selbst steckt in Core (<see cref="Bandauswahl"/> und
/// <c>BandauswahlTests</c>); hier geht es um das, was erst im
/// Zusammenspiel mit den Knoepfen entsteht.
/// </summary>
public class BandzoneTests
{
    private static Bandeintrag Eintrag(
        string schluessel, Bandrang rang, string titel, Action? beimSchliessen = null)
        => new()
        {
            Schluessel = schluessel,
            Meldung = new Bandmeldung(rang, titel, "Eine Kurzzeile.", "Der lange Text."),
            BeimSchliessen = beimSchliessen,
        };

    [Fact]
    public void Ohne_Meldung_ist_die_Bandzone_unsichtbar()
    {
        Assert.False(new BaenderViewModel().Sichtbar);
    }

    [Fact]
    public void Das_dringendste_Band_steht_vorn()
    {
        var zone = new BaenderViewModel();

        zone.Zeige(Eintrag("a", Bandrang.Hinweis, "Hinweis"));
        zone.Zeige(Eintrag("b", Bandrang.Fehler, "Fehler"));

        Assert.True(zone.Sichtbar);
        // Gezeigt wird der Fehler, und er ist die ERSTE von zwei
        // Meldungen: gezaehlt wird in der Rangfolge, nicht in der
        // Reihenfolge des Eintreffens.
        Assert.Equal("Fehler", zone.Titel);
        Assert.Equal("1 von 2", zone.Zaehltext);
    }

    // Derselbe Absender zeigt nie zwei Baender zugleich: die Meldung
    // "Neustart hat nicht geklappt" loest "Fassung ist bereit" ab, statt
    // sich daneben zu stellen.
    [Fact]
    public void Derselbe_Absender_loest_sein_eigenes_Band_ab()
    {
        var zone = new BaenderViewModel();

        zone.Zeige(Eintrag("aktualisierung", Bandrang.Hinweis, "Fassung bereit"));
        zone.Zeige(Eintrag("aktualisierung", Bandrang.Warnung, "Neustart misslungen"));

        Assert.Equal("Neustart misslungen", zone.Titel);

        // Kein Zaehler: es ist bei EINEM Band geblieben.
        Assert.False(zone.MehrereVorhanden);
    }

    [Fact]
    public void Schliessen_nimmt_nur_das_gezeigte_Band_weg()
    {
        var zone = new BaenderViewModel();

        zone.Zeige(Eintrag("a", Bandrang.Hinweis, "Hinweis"));
        zone.Zeige(Eintrag("b", Bandrang.Fehler, "Fehler"));

        zone.SchliessenCommand.Execute(null);

        Assert.True(zone.Sichtbar);
        Assert.Equal("Hinweis", zone.Titel);
        Assert.False(zone.MehrereVorhanden);
    }

    [Fact]
    public void Nach_dem_letzten_Band_ist_die_Zone_wieder_leer()
    {
        var zone = new BaenderViewModel();
        zone.Zeige(Eintrag("a", Bandrang.Hinweis, "Hinweis"));

        zone.SchliessenCommand.Execute(null);

        Assert.False(zone.Sichtbar);
        Assert.Equal(string.Empty, zone.Titel);
    }

    // Das ist die Abmachung hinter "Später": der Absender erfaehrt, dass
    // sein Band weggeklickt wurde, und kann sich das merken.
    [Fact]
    public void Der_Absender_erfaehrt_vom_Schliessen()
    {
        var gemerkt = false;
        var zone = new BaenderViewModel();

        zone.Zeige(Eintrag("a", Bandrang.Hinweis, "Hinweis", () => gemerkt = true));
        zone.SchliessenCommand.Execute(null);

        Assert.True(gemerkt);
    }

    [Fact]
    public void Weiterblaettern_laeuft_im_Kreis()
    {
        var zone = new BaenderViewModel();

        zone.Zeige(Eintrag("a", Bandrang.Fehler, "Fehler"));
        zone.Zeige(Eintrag("b", Bandrang.Hinweis, "Hinweis"));

        Assert.Equal("Fehler", zone.Titel);

        zone.WeiterCommand.Execute(null);
        Assert.Equal("Hinweis", zone.Titel);

        zone.WeiterCommand.Execute(null);
        Assert.Equal("Fehler", zone.Titel);
    }

    // Ein neues Band springt nach vorn - sonst muesste der Anwender erst
    // blaettern, um zu sehen, was gerade passiert ist.
    [Fact]
    public void Ein_neues_Band_holt_den_Blaetterstand_nach_vorn()
    {
        var zone = new BaenderViewModel();

        zone.Zeige(Eintrag("a", Bandrang.Fehler, "Fehler"));
        zone.Zeige(Eintrag("b", Bandrang.Hinweis, "alter Hinweis"));
        zone.WeiterCommand.Execute(null);

        Assert.Equal("alter Hinweis", zone.Titel);

        zone.Zeige(Eintrag("c", Bandrang.Warnung, "neue Warnung"));

        // Vorn steht wieder das dringendste - der Blaetterstand ist
        // zurueckgesetzt.
        Assert.Equal("Fehler", zone.Titel);
    }

    [Fact]
    public void Details_zeigen_den_langen_Text_und_lassen_sich_schliessen()
    {
        var zone = new BaenderViewModel();
        zone.Zeige(Eintrag("a", Bandrang.Hinweis, "Hinweis"));

        Assert.False(zone.DetailsAktiv);
        Assert.True(zone.DetailsVorhanden);

        zone.DetailsCommand.Execute(null);

        Assert.True(zone.DetailsAktiv);
        Assert.Equal("Hinweis", zone.DetailsTitel);
        Assert.Equal("Der lange Text.", zone.DetailsText);

        zone.DetailsSchliessenCommand.Execute(null);
        Assert.False(zone.DetailsAktiv);
    }

    [Fact]
    public void Ohne_Aktion_bleibt_der_hervorgehobene_Knopf_aus()
    {
        var zone = new BaenderViewModel();
        zone.Zeige(Eintrag("a", Bandrang.Hinweis, "Hinweis"));

        Assert.False(zone.AktionVorhanden);
    }

    [Fact]
    public void Die_Aktion_des_gezeigten_Bandes_wird_ausgeloest()
    {
        var ausgeloest = 0;
        var zone = new BaenderViewModel();

        zone.Zeige(new Bandeintrag
        {
            Schluessel = "a",
            Meldung = new Bandmeldung(Bandrang.Hinweis, "Hinweis", "Kurz.", "Lang."),
            AktionText = "Jetzt neu starten",
            Aktion = () => ausgeloest++,
        });

        Assert.True(zone.AktionVorhanden);
        Assert.Equal("Jetzt neu starten", zone.AktionText);

        zone.AusfuehrenCommand.Execute(null);

        Assert.Equal(1, ausgeloest);
    }

    // Die Farbe des Bandes haengt am Rang der GEZEIGTEN Meldung - sonst
    // stuende ein Fehler auf blauem Grund, sobald man weiterblaettert.
    [Fact]
    public void Die_Rangkennzeichnung_folgt_dem_gezeigten_Band()
    {
        var zone = new BaenderViewModel();

        zone.Zeige(Eintrag("a", Bandrang.Fehler, "Fehler"));
        zone.Zeige(Eintrag("b", Bandrang.Hinweis, "Hinweis"));

        Assert.True(zone.IstFehler);
        Assert.False(zone.IstHinweis);

        zone.WeiterCommand.Execute(null);

        Assert.True(zone.IstHinweis);
        Assert.False(zone.IstFehler);
    }

    [Fact]
    public void Ein_zurueckgenommenes_Band_verschwindet()
    {
        var zone = new BaenderViewModel();
        zone.Zeige(Eintrag("a", Bandrang.Hinweis, "Hinweis"));

        zone.Entferne("a");

        Assert.False(zone.Sichtbar);
    }
}
