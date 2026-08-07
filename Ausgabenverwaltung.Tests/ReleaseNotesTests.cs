using Ausgabenverwaltung.Core.Updates;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Geprueft wird gegen den Text, den GitHub bei diesem Vorhaben
/// tatsaechlich erzeugt (<c>gh release create --generate-notes</c>, siehe
/// PUBLISH.md) - eine selbst erfundene Beschreibung haette genau die Form,
/// die der Leser ohnehin erwartet, und wuerde nichts beweisen.
/// </summary>
public class ReleaseNotesTests
{
    // Aufbau und Schreibweise unveraendert, gekuerzt auf drei Zeilen.
    private const string EchterText = """
        ## What's Changed
        * Betragsfeld rechnet, Datumsfeld versteht Kurzformen by @philip1302 in https://github.com/philip1302/Ausgabenverwaltung/pull/12
        * Aus einer Buchung laesst sich eine Vorlage anlegen by @philip1302 in https://github.com/philip1302/Ausgabenverwaltung/pull/13

        **Full Changelog**: https://github.com/philip1302/Ausgabenverwaltung/compare/v1.1.0...v1.2.0
        """;

    [Fact]
    public void Der_echte_Text_wird_zu_Ueberschrift_und_Punkten()
    {
        var abschnitte = ReleaseNotes.Lies(EchterText);

        Assert.Equal(3, abschnitte.Count);

        Assert.Equal(ReleaseNoteArt.Ueberschrift, abschnitte[0].Art);
        Assert.Equal("What's Changed", abschnitte[0].Text);

        Assert.Equal(ReleaseNoteArt.Punkt, abschnitte[1].Art);
        Assert.Equal("Betragsfeld rechnet, Datumsfeld versteht Kurzformen", abschnitte[1].Text);

        Assert.Equal(ReleaseNoteArt.Punkt, abschnitte[2].Art);
        Assert.Equal("Aus einer Buchung laesst sich eine Vorlage anlegen", abschnitte[2].Text);
    }

    // Der Anmeldename und die Adresse dahinter sagen dem Anwender nichts -
    // in einem Vorhaben mit einem einzigen Verfasser schon gar nicht.
    [Fact]
    public void Anmeldename_und_Adresse_bleiben_draussen()
    {
        var abschnitte = ReleaseNotes.Lies(EchterText);

        Assert.DoesNotContain(abschnitte, a => a.Text.Contains('@'));
        Assert.DoesNotContain(abschnitte, a => a.Text.Contains("http", StringComparison.Ordinal));
    }

    // "**Full Changelog**: <Adresse>" bliebe sonst als blosse
    // Beschriftung ohne Inhalt stehen.
    [Fact]
    public void Eine_Zeile_die_nur_auf_eine_Adresse_verweist_faellt_weg()
    {
        var abschnitte = ReleaseNotes.Lies(EchterText);

        Assert.DoesNotContain(abschnitte, a => a.Text.Contains("Full Changelog", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\n  ")]
    public void Ohne_Text_gibt_es_keine_Abschnitte(string? koerper)
    {
        Assert.Empty(ReleaseNotes.Lies(koerper));
    }

    [Theory]
    [InlineData("- mit Strich")]
    [InlineData("* mit Stern")]
    [InlineData("+ mit Plus")]
    [InlineData("1. mit Zahl")]
    [InlineData("2) mit Klammer")]
    public void Alle_ueblichen_Aufzaehlungszeichen_werden_erkannt(string zeile)
    {
        var abschnitt = Assert.Single(ReleaseNotes.Lies(zeile));

        Assert.Equal(ReleaseNoteArt.Punkt, abschnitt.Art);
        Assert.StartsWith("mit ", abschnitt.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Auszeichnungen_fallen_weg_der_Text_bleibt()
    {
        var abschnitte = ReleaseNotes.Lies(
            "Der **Betrag** wird jetzt in `Cent` gerechnet, siehe [Regel 1](https://example.org/regeln).");

        var abschnitt = Assert.Single(abschnitte);

        Assert.Equal(ReleaseNoteArt.Absatz, abschnitt.Art);
        Assert.Equal(
            "Der Betrag wird jetzt in Cent gerechnet, siehe Regel 1.",
            abschnitt.Text);
    }

    // Einzelne Sterne und Unterstriche kommen in Bezeichnern und
    // Dateinamen vor - wer sie herausschneidet, veraendert den Text.
    [Fact]
    public void Einzelne_Sterne_und_Unterstriche_bleiben_stehen()
    {
        var abschnitt = Assert.Single(ReleaseNotes.Lies("Die Datei migration_v3_to_v4.sql kam dazu."));

        Assert.Equal("Die Datei migration_v3_to_v4.sql kam dazu.", abschnitt.Text);
    }

    [Fact]
    public void Fliesstext_ueber_mehrere_Zeilen_wird_ein_Absatz()
    {
        var abschnitte = ReleaseNotes.Lies("""
            Diese Fassung raeumt die
            Datensicherung auf.

            Danach kommt der Rest.
            """);

        Assert.Equal(2, abschnitte.Count);
        Assert.Equal("Diese Fassung raeumt die Datensicherung auf.", abschnitte[0].Text);
        Assert.Equal("Danach kommt der Rest.", abschnitte[1].Text);
    }

    [Fact]
    public void Ueberschriften_jeder_Ebene_und_Trennlinien()
    {
        var abschnitte = ReleaseNotes.Lies("""
            # Fassung 1.2.0
            ---
            ### Kleingedrucktes
            """);

        Assert.Equal(2, abschnitte.Count);
        Assert.All(abschnitte, a => Assert.Equal(ReleaseNoteArt.Ueberschrift, a.Art));
        Assert.Equal("Fassung 1.2.0", abschnitte[0].Text);
        Assert.Equal("Kleingedrucktes", abschnitte[1].Text);
    }

    // Die Grenze schuetzt die Anzeige vor einem Text, der aus welchem
    // Grund auch immer ausufert.
    [Fact]
    public void Mehr_als_die_Hoechstzahl_wird_abgeschnitten()
    {
        var viele = string.Join(
            "\n",
            Enumerable.Range(0, ReleaseNotes.Hoechstzahl + 50).Select(i => $"* Punkt {i}"));

        Assert.Equal(ReleaseNotes.Hoechstzahl, ReleaseNotes.Lies(viele).Count);
    }
}
