using Ausgabenverwaltung.Core.Updates;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Regeln fuer die Seite "Was ist neu": wann sie erscheint, wann sie
/// bewusst still bleibt, und was danach als gesehen gemerkt wird.
/// </summary>
public class WasIstNeuTests
{
    private const string Notizen = """
        ## What's Changed
        * Markierte Buchungen lassen sich gemeinsam aendern
        """;

    [Fact]
    public void Nach_einer_Aktualisierung_erscheint_die_Seite()
    {
        var entscheidung = WasIstNeu.Treffe("1.2.0", "1.1.0", "v1.2.0", Notizen);

        Assert.True(entscheidung.Zeigen);
        Assert.Equal("1.2.0", entscheidung.VersionText);
        Assert.Equal("1.2.0", entscheidung.MerkeVersion);
        Assert.Equal(2, entscheidung.Abschnitte.Count);
    }

    // Der Regelfall bei jedem gewoehnlichen Start: nichts zu zeigen und
    // nichts zu schreiben.
    [Fact]
    public void Bei_gleicher_Fassung_bleibt_alles_still()
    {
        var entscheidung = WasIstNeu.Treffe("1.2.0", "1.2.0", "v1.2.0", Notizen);

        Assert.False(entscheidung.Zeigen);
        Assert.Null(entscheidung.MerkeVersion);
    }

    // Die Baugruppe traegt den Git-Stand hinter einem Pluszeichen, die
    // Release-Marke ein fuehrendes "v" - beides darf hier nicht stoeren.
    [Fact]
    public void Bauzusatz_und_Marke_werden_verstanden()
    {
        var entscheidung = WasIstNeu.Treffe(
            "1.2.0+c63496bec93093cc2c9f4fc0cf62ecc2abdfb345", "1.1.0", "v1.2.0", Notizen);

        Assert.True(entscheidung.Zeigen);
        Assert.Equal("1.2.0", entscheidung.MerkeVersion);
    }

    // Wer noch nichts Altes kennt, braucht keine Liste der Aenderungen
    // daran - gemerkt wird trotzdem, sonst zaehlte die naechste
    // Aktualisierung von vorn auf.
    [Fact]
    public void Die_erste_Ausfuehrung_zeigt_nichts_merkt_sich_aber_den_Stand()
    {
        var entscheidung = WasIstNeu.Treffe("1.2.0", null, "v1.2.0", Notizen);

        Assert.False(entscheidung.Zeigen);
        Assert.Equal("1.2.0", entscheidung.MerkeVersion);
    }

    [Fact]
    public void Ohne_gemerkten_Beschreibungstext_bleibt_die_Seite_aus()
    {
        var entscheidung = WasIstNeu.Treffe("1.2.0", "1.1.0", null, null);

        Assert.False(entscheidung.Zeigen);
        Assert.Empty(entscheidung.Abschnitte);
        Assert.Equal("1.2.0", entscheidung.MerkeVersion);
    }

    // Ein Text, der zu einer anderen Fassung gehoert, beschriebe etwas
    // anderes als das, was gerade laeuft.
    [Fact]
    public void Ein_Text_zur_falschen_Fassung_wird_nicht_gezeigt()
    {
        var entscheidung = WasIstNeu.Treffe("1.3.0", "1.1.0", "v1.2.0", Notizen);

        Assert.False(entscheidung.Zeigen);
        Assert.Equal("1.3.0", entscheidung.MerkeVersion);
    }

    [Fact]
    public void Ein_leerer_Beschreibungstext_ergibt_keine_Seite()
    {
        var entscheidung = WasIstNeu.Treffe("1.2.0", "1.1.0", "v1.2.0", "   ");

        Assert.False(entscheidung.Zeigen);
        Assert.Equal("1.2.0", entscheidung.MerkeVersion);
    }

    // Von Hand auf eine aeltere Fassung zurueckgegangen: nichts zeigen,
    // aber den Stand nachziehen, damit ein spaeteres Wiederhochgehen die
    // Seite erneut zeigt.
    [Fact]
    public void Ein_Rueckschritt_zieht_den_gemerkten_Stand_nach()
    {
        var entscheidung = WasIstNeu.Treffe("1.1.0", "1.2.0", "v1.2.0", Notizen);

        Assert.False(entscheidung.Zeigen);
        Assert.Equal("1.1.0", entscheidung.MerkeVersion);
    }

    // Ohne lesbare eigene Version laesst sich nichts vergleichen - dann
    // wird auch nichts gemerkt, sonst stuende hinterher Unsinn in den
    // Einstellungen.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unbekannt")]
    public void Ohne_lesbare_eigene_Version_passiert_nichts(string? laufend)
    {
        var entscheidung = WasIstNeu.Treffe(laufend, "1.1.0", "v1.2.0", Notizen);

        Assert.False(entscheidung.Zeigen);
        Assert.Null(entscheidung.MerkeVersion);
    }

    // Ein unlesbarer gemerkter Stand wird wie "noch nichts gemerkt"
    // behandelt: still, aber nachgezogen.
    [Fact]
    public void Ein_unlesbarer_gemerkter_Stand_wird_ueberschrieben()
    {
        var entscheidung = WasIstNeu.Treffe("1.2.0", "kaputt", "v1.2.0", Notizen);

        Assert.False(entscheidung.Zeigen);
        Assert.Equal("1.2.0", entscheidung.MerkeVersion);
    }
}
