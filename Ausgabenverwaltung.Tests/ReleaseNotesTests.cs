using Ausgabenverwaltung.Core.Updates;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Der kleine Markdown-Ausschnitt, den die Aenderungsliste benutzt:
/// Ueberschrift, Aufzaehlungspunkt, Absatz. Mehr Form gibt es nicht, und
/// das ist Absicht (siehe <see cref="ReleaseNotes"/>).
/// </summary>
public class ReleaseNotesTests
{
    [Fact]
    public void Ueberschrift_Punkt_und_Absatz_werden_unterschieden()
    {
        var abschnitte = ReleaseNotes.Lies("""
            ### Weniger Tipparbeit

            Das Wichtigste zuerst.

            - Das Betragsfeld rechnet jetzt.
            - Das Datumsfeld versteht „heute".
            """);

        Assert.Equal(4, abschnitte.Count);

        Assert.Equal(ReleaseNoteArt.Ueberschrift, abschnitte[0].Art);
        Assert.Equal("Weniger Tipparbeit", abschnitte[0].Text);

        Assert.Equal(ReleaseNoteArt.Absatz, abschnitte[1].Art);
        Assert.Equal("Das Wichtigste zuerst.", abschnitte[1].Text);

        Assert.Equal(ReleaseNoteArt.Punkt, abschnitte[2].Art);
        Assert.Equal("Das Betragsfeld rechnet jetzt.", abschnitte[2].Text);
        Assert.Equal(ReleaseNoteArt.Punkt, abschnitte[3].Art);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\n  ")]
    public void Ohne_Text_gibt_es_keine_Abschnitte(string? text)
    {
        Assert.Empty(ReleaseNotes.Lies(text));
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
            "Der **Betrag** wird in `Cent` gerechnet, siehe [die Regeln](https://example.org/regeln).");

        var abschnitt = Assert.Single(abschnitte);

        Assert.Equal(ReleaseNoteArt.Absatz, abschnitt.Art);
        Assert.Equal(
            "Der Betrag wird in Cent gerechnet, siehe die Regeln.",
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

    // Eine Datei mit kurzen Zeilen bricht Aufzaehlungspunkte um. Aus
    // einem umgebrochenen Punkt darf nicht ein Punkt plus ein
    // angefangener Absatz werden.
    [Fact]
    public void Ein_umgebrochener_Aufzaehlungspunkt_bleibt_ein_Punkt()
    {
        var abschnitte = ReleaseNotes.Lies("""
            - Das Betragsfeld rechnet jetzt: „12,50+3,20" wird zu 15,70 €,
              damit sich drei Kassenzettel zusammenzählen lassen.
            - Der zweite Punkt.
            """);

        Assert.Equal(2, abschnitte.Count);
        Assert.All(abschnitte, a => Assert.Equal(ReleaseNoteArt.Punkt, a.Art));
        Assert.Equal(
            "Das Betragsfeld rechnet jetzt: „12,50+3,20\" wird zu 15,70 €, "
            + "damit sich drei Kassenzettel zusammenzählen lassen.",
            abschnitte[0].Text);
    }

    // Ein Absatz darf im Quelltext umgebrochen sein, ohne dass daraus zwei
    // Zeilen auf der Seite werden.
    [Fact]
    public void Fliesstext_ueber_mehrere_Zeilen_wird_ein_Absatz()
    {
        var abschnitte = ReleaseNotes.Lies("""
            Diese Fassung räumt die
            Datensicherung auf.

            Danach kommt der Rest.
            """);

        Assert.Equal(2, abschnitte.Count);
        Assert.Equal("Diese Fassung räumt die Datensicherung auf.", abschnitte[0].Text);
        Assert.Equal("Danach kommt der Rest.", abschnitte[1].Text);
    }

    [Fact]
    public void Trennlinien_erzeugen_keinen_Abschnitt()
    {
        var abschnitte = ReleaseNotes.Lies("""
            # Fassung 1.4.0
            ---
            ### Kleingedrucktes
            """);

        Assert.Equal(2, abschnitte.Count);
        Assert.All(abschnitte, a => Assert.Equal(ReleaseNoteArt.Ueberschrift, a.Art));
        Assert.Equal("Fassung 1.4.0", abschnitte[0].Text);
        Assert.Equal("Kleingedrucktes", abschnitte[1].Text);
    }

    // Die Grenze schuetzt die Anzeige vor einem Text, der aus welchem
    // Grund auch immer ausufert.
    [Fact]
    public void Mehr_als_die_Hoechstzahl_wird_abgeschnitten()
    {
        var viele = string.Join(
            "\n",
            Enumerable.Range(0, ReleaseNotes.Hoechstzahl + 50).Select(i => $"- Punkt {i}"));

        Assert.Equal(ReleaseNotes.Hoechstzahl, ReleaseNotes.Lies(viele).Count);
    }
}
