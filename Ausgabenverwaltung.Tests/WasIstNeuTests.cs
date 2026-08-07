using Ausgabenverwaltung.Core.Updates;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Regeln fuer die Seite "Was ist neu": wann sie erscheint, was sie
/// dann zeigt, wann sie bewusst still bleibt, und was danach als gesehen
/// gemerkt wird.
/// </summary>
public class WasIstNeuTests
{
    private const string Aenderungsliste = """
        # Was ist neu

        Ein Vorwort, das nicht mitgezeigt wird.

        ## 1.4.0 — 07.08.2026

        - Das Betragsfeld rechnet jetzt.

        ## 1.3.0 — 01.08.2026

        - Die Datensicherung zeigt einen Zustand.
        """;

    [Fact]
    public void Nach_einer_Aktualisierung_erscheint_der_Abschnitt_der_neuen_Fassung()
    {
        var entscheidung = WasIstNeu.Treffe("1.4.0", "1.3.0", Aenderungsliste);

        Assert.True(entscheidung.Zeigen);
        Assert.Equal("1.4.0", entscheidung.VersionText);
        Assert.Equal("1.4.0", entscheidung.MerkeVersion);

        var abschnitt = Assert.Single(entscheidung.Abschnitte);
        Assert.Equal("Das Betragsfeld rechnet jetzt.", abschnitt.Text);
    }

    // Wer eine Fassung ueberspringt, soll nichts verpassen - beide
    // Abschnitte, jeder mit seiner Ueberschrift.
    [Fact]
    public void Uebersprungene_Fassungen_werden_nachgeholt()
    {
        var entscheidung = WasIstNeu.Treffe("1.4.0", "1.2.0", Aenderungsliste);

        Assert.True(entscheidung.Zeigen);

        var texte = entscheidung.Abschnitte.Select(a => a.Text).ToList();

        Assert.Equal(
            [
                "Fassung 1.4.0 — 07.08.2026",
                "Das Betragsfeld rechnet jetzt.",
                "Fassung 1.3.0 — 01.08.2026",
                "Die Datensicherung zeigt einen Zustand.",
            ],
            texte);
    }

    // Bei genau einer Fassung nennt schon der Seitentitel sie - die
    // Ueberschrift stuende doppelt.
    [Fact]
    public void Bei_einer_einzigen_Fassung_steht_keine_Fassungsueberschrift()
    {
        var entscheidung = WasIstNeu.Treffe("1.4.0", "1.3.0", Aenderungsliste);

        Assert.DoesNotContain(
            entscheidung.Abschnitte,
            a => a.Text.StartsWith("Fassung", StringComparison.Ordinal));
    }

    // Eine Fassung, die neuer ist als die laufende, gehoert nicht dazu -
    // sonst kuendigte die Seite an, was der Anwender gar nicht hat.
    [Fact]
    public void Neuere_Fassungen_als_die_laufende_bleiben_draussen()
    {
        var entscheidung = WasIstNeu.Treffe("1.3.0", "1.2.0", Aenderungsliste);

        Assert.True(entscheidung.Zeigen);

        var abschnitt = Assert.Single(entscheidung.Abschnitte);
        Assert.Equal("Die Datensicherung zeigt einen Zustand.", abschnitt.Text);
    }

    // Der Regelfall bei jedem gewoehnlichen Start: nichts zu zeigen und
    // nichts zu schreiben.
    [Fact]
    public void Bei_gleicher_Fassung_bleibt_alles_still()
    {
        var entscheidung = WasIstNeu.Treffe("1.4.0", "1.4.0", Aenderungsliste);

        Assert.False(entscheidung.Zeigen);
        Assert.Null(entscheidung.MerkeVersion);
    }

    // Die Baugruppe traegt den Git-Stand hinter einem Pluszeichen - das
    // darf hier nicht stoeren.
    [Fact]
    public void Der_Bauzusatz_der_eigenen_Version_wird_verstanden()
    {
        var entscheidung = WasIstNeu.Treffe(
            "1.4.0+c63496bec93093cc2c9f4fc0cf62ecc2abdfb345", "1.3.0", Aenderungsliste);

        Assert.True(entscheidung.Zeigen);
        Assert.Equal("1.4.0", entscheidung.MerkeVersion);
    }

    // Wer noch nichts Altes kennt, braucht keine Liste der Aenderungen
    // daran - gemerkt wird trotzdem, sonst zaehlte die naechste
    // Aktualisierung von vorn auf.
    [Fact]
    public void Die_erste_Ausfuehrung_zeigt_nichts_merkt_sich_aber_den_Stand()
    {
        var entscheidung = WasIstNeu.Treffe("1.4.0", null, Aenderungsliste);

        Assert.False(entscheidung.Zeigen);
        Assert.Equal("1.4.0", entscheidung.MerkeVersion);
    }

    // Der Fall, den CLAUDE.md Regel 15 verhindern soll: Version angehoben,
    // aber kein Abschnitt geschrieben. Eine leere Seite waere schlechter
    // als keine.
    [Fact]
    public void Eine_Fassung_ohne_eigenen_Abschnitt_zeigt_nichts()
    {
        var entscheidung = WasIstNeu.Treffe("1.5.0", "1.4.0", Aenderungsliste);

        Assert.False(entscheidung.Zeigen);
        Assert.Empty(entscheidung.Abschnitte);
        Assert.Equal("1.5.0", entscheidung.MerkeVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("# Was ist neu\n\nNoch nichts.")]
    public void Ohne_brauchbare_Aenderungsliste_bleibt_die_Seite_aus(string? liste)
    {
        var entscheidung = WasIstNeu.Treffe("1.4.0", "1.3.0", liste);

        Assert.False(entscheidung.Zeigen);
        Assert.Equal("1.4.0", entscheidung.MerkeVersion);
    }

    // Von Hand auf eine aeltere Fassung zurueckgegangen: nichts zeigen,
    // aber den Stand nachziehen, damit ein spaeteres Wiederhochgehen die
    // Seite erneut zeigt.
    [Fact]
    public void Ein_Rueckschritt_zieht_den_gemerkten_Stand_nach()
    {
        var entscheidung = WasIstNeu.Treffe("1.3.0", "1.4.0", Aenderungsliste);

        Assert.False(entscheidung.Zeigen);
        Assert.Equal("1.3.0", entscheidung.MerkeVersion);
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
        var entscheidung = WasIstNeu.Treffe(laufend, "1.3.0", Aenderungsliste);

        Assert.False(entscheidung.Zeigen);
        Assert.Null(entscheidung.MerkeVersion);
    }

    // Ein unlesbarer gemerkter Stand wird wie "noch nichts gemerkt"
    // behandelt: still, aber nachgezogen.
    [Fact]
    public void Ein_unlesbarer_gemerkter_Stand_wird_ueberschrieben()
    {
        var entscheidung = WasIstNeu.Treffe("1.4.0", "kaputt", Aenderungsliste);

        Assert.False(entscheidung.Zeigen);
        Assert.Equal("1.4.0", entscheidung.MerkeVersion);
    }
}
