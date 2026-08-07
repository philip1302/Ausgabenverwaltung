using System.Reflection;
using Ausgabenverwaltung.Core.Updates;
using Ausgabenverwaltung.ViewModels;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Aenderungsliste (CHANGELOG.md) - einmal als Textverarbeitung und
/// einmal als Zusage an den Anwender.
///
/// Der zweite Teil ist ungewoehnlich, weil er Sprache prueft statt
/// Verhalten, und steht hier aus demselben Grund wie
/// <see cref="MeldungsGrundsaetzeTests"/>: dieser Text IST das Erzeugnis,
/// und ohne Pruefung faellt niemandem auf, wenn eines Tages
/// "ViewModel-Umbau" darin steht. Zusammen bilden diese Tests die
/// pruefbare Haelfte von CLAUDE.md, Regel 15 - die andere Haelfte
/// (verstaendlich geschrieben) kann nur ein Mensch beurteilen.
/// </summary>
public class ChangelogTests
{
    // Woerter aus der Werkstatt. Sie moegen zutreffen, aber sie
    // beantworten nicht die einzige Frage, die der Anwender an diese
    // Liste hat: was ist fuer mich anders?
    private static readonly string[] Fachgesimpel =
    [
        "ViewModel", "Repository", "Commit", "Merge", "Refactoring", "SQL",
        "Schnittstelle", "Klasse", "Methode", "Namespace", "Exception",
        "Bugfix", "Regex", "API", "Migration", "Transaktion",
    ];

    private static IReadOnlyList<ChangelogEintrag> Echte()
        => Changelog.Lies(Changelog.Eingebettet());

    // ---------------- Die echte Datei ----------------

    [Fact]
    public void Die_Aenderungsliste_ist_eingebettet_und_lesbar()
    {
        Assert.NotNull(Changelog.Eingebettet());
        Assert.NotEmpty(Echte());
    }

    /// <summary>
    /// Der eigentliche Waechter fuer Regel 15: wer die Versionsnummer
    /// anhebt, ohne den Abschnitt dazu zu schreiben, faellt hier auf -
    /// und nicht erst dem Anwender, dem die Seite "Was ist neu" dann
    /// stillschweigend ausbliebe.
    /// </summary>
    [Fact]
    public void Zu_der_ausgelieferten_Fassung_gibt_es_einen_Abschnitt()
    {
        var ausgeliefert = typeof(WasIstNeuViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Assert.True(
            AppVersion.TryParse(ausgeliefert, out var version),
            $"Die Fassung „{ausgeliefert}“ lässt sich nicht lesen.");

        Assert.True(
            Echte().Any(eintrag => eintrag.Version == version),
            $"CHANGELOG.md hat keinen Abschnitt „## {version}“. "
            + "Vor dem Anheben der Versionsnummer gehört er geschrieben "
            + "(CLAUDE.md, Regel 15).");
    }

    [Fact]
    public void Die_neueste_Fassung_steht_vorn()
    {
        var versionen = Echte().Select(eintrag => eintrag.Version).ToList();

        Assert.Equal(versionen.OrderByDescending(v => v).ToList(), versionen);
    }

    [Fact]
    public void Kein_Abschnitt_ist_leer()
    {
        Assert.All(Echte(), eintrag => Assert.NotEmpty(eintrag.Inhalt));
    }

    /// <summary>
    /// Der Sammelplatz fuer das, was noch in keiner Fassung steckt
    /// (CLAUDE.md, Regel 15a). Er traegt keine lesbare Versionsnummer und
    /// darf deshalb nirgends auftauchen - anzukuendigen, was der Anwender
    /// noch gar nicht hat, waere schlimmer als zu schweigen.
    /// </summary>
    [Fact]
    public void Was_noch_nicht_veroeffentlicht_ist_wird_niemandem_gezeigt()
    {
        var eintraege = Changelog.Lies("""
            ## Unveröffentlicht

            - Etwas, das es noch nicht gibt.

            ## 1.4.0 — 07.08.2026

            - Etwas Fertiges.
            """);

        var eintrag = Assert.Single(eintraege);

        Assert.Equal(new Version(1, 4, 0), eintrag.Version);
        Assert.DoesNotContain(
            eintrag.Inhalt,
            block => block.Text.Contains("noch nicht gibt", StringComparison.Ordinal));
    }

    // ---------------- Die Zusage an den Anwender ----------------

    [Fact]
    public void Was_der_Anwender_liest_enthaelt_kein_Fachgesimpel()
    {
        foreach (var zeile in AlleZeilen())
        {
            foreach (var wort in Fachgesimpel)
            {
                Assert.False(
                    zeile.Contains(wort, StringComparison.OrdinalIgnoreCase),
                    $"„{zeile}“ enthält den Werkstattbegriff „{wort}“.");
            }
        }
    }

    [Fact]
    public void Jede_Aussage_ist_ein_ganzer_Satz()
    {
        foreach (var zeile in AlleZeilen(nurAussagen: true))
        {
            Assert.True(
                zeile.EndsWith('.') || zeile.EndsWith('!') || zeile.EndsWith('?'),
                $"„{zeile}“ ist kein abgeschlossener Satz.");

            Assert.True(
                char.IsUpper(zeile[0]) || char.IsDigit(zeile[0]) || zeile[0] == '„',
                $"„{zeile}“ beginnt nicht mit einem Großbuchstaben.");
        }
    }

    // ---------------- Der Leser ----------------

    private const string Beispiel = """
        # Was ist neu

        Ein Vorwort, das nicht zu einer Fassung gehoert.

        ## 1.4.0 — 07.08.2026

        - Etwas Neues.

        ## nicht lesbar

        - Faellt weg, weil die Ueberschrift keine Version ist.

        ## 1.2.0 — 01.07.2026

        ## 1.3.0 — 01.08.2026

        - Etwas Aelteres.
        """;

    [Fact]
    public void Das_Vorwort_gehoert_zu_keiner_Fassung()
    {
        var eintraege = Changelog.Lies(Beispiel);

        Assert.DoesNotContain(
            eintraege.SelectMany(e => e.Inhalt),
            block => block.Text.Contains("Vorwort", StringComparison.Ordinal));
    }

    [Fact]
    public void Nur_lesbare_Fassungen_mit_Inhalt_kommen_durch()
    {
        var eintraege = Changelog.Lies(Beispiel);

        // "nicht lesbar" faellt heraus (keine Version), "1.2.0"
        // ebenfalls (kein Inhalt) - und sortiert wird trotz der
        // vertauschten Reihenfolge in der Datei.
        Assert.Equal(
            [new Version(1, 4, 0), new Version(1, 3, 0)],
            eintraege.Select(e => e.Version).ToList());
    }

    [Fact]
    public void Der_Titel_traegt_Fassung_und_Datum()
    {
        var eintraege = Changelog.Lies(Beispiel);

        Assert.Equal("1.4.0 — 07.08.2026", eintraege[0].Titel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Nur Fliesstext ohne jede Fassung.")]
    public void Ohne_brauchbaren_Text_gibt_es_keine_Eintraege(string? text)
    {
        Assert.Empty(Changelog.Lies(text));
    }

    private static IEnumerable<string> AlleZeilen(bool nurAussagen = false)
        => Echte()
            .SelectMany(eintrag => eintrag.Inhalt)
            .Where(block => !nurAussagen || block.Art != ReleaseNoteArt.Ueberschrift)
            .Select(block => block.Text);
}
