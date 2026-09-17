using Ausgabenverwaltung.Core.Errors;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Formregeln der Hinweisbaender.
///
/// Sie stehen hier, weil genau diese Form das Problem war, das den Umbau
/// ausgeloest hat: das Aktualisierungsband trug drei Absaetze und eine
/// nummerierte Anleitung in EINEM Textblock ueber die volle Fensterbreite.
/// Ein Band ist aber eine Statuszeile und kein Absatz - waechst es wieder,
/// ist der Umbau umsonst gewesen.
///
/// Diese Tests sind deshalb bewusst Laengen- und Formpruefungen und keine
/// Verhaltenspruefungen. Sie halten fest, was sich sonst still
/// zurueckschleicht.
/// </summary>
public class BandmeldungTests
{
    // Der Titel steht einzeilig neben Icon und Knoepfen. Laenger als das
    // wird er bei schmalem Fenster oder grosser Schrift abgeschnitten -
    // und ein abgeschnittener Titel sagt nichts mehr.
    private const int TitelHoechstlaenge = 60;

    // Die Kurzzeile darf zwei Zeilen fuellen, mehr nicht. 140 Zeichen sind
    // bei der kleinsten sinnvollen Bandbreite ungefaehr das.
    private const int KurzHoechstlaenge = 140;

    public static TheoryData<string, Bandmeldung> AlleBaender()
    {
        var daten = new TheoryData<string, Bandmeldung>();

        daten.Add("Bereitgelegt", UpdateText.Bereitgelegt("1.2.0"));
        daten.Add("NeustartGescheitert", UpdateText.NeustartGescheitert());

        foreach (var hindernis in Enum.GetValues<UpdateHindernis>())
        {
            daten.Add($"Hinweis/{hindernis}", UpdateText.NurHinweis("1.2.0", hindernis));
            daten.Add($"HinweisDatei/{hindernis}", UpdateText.NurHinweis(
                "1.2.0", hindernis, "Ausgabenverwaltung-win-x64.zip"));
            daten.Add($"HinweisBundle/{hindernis}", UpdateText.NurHinweis(
                "1.2.0", hindernis, "Ausgabenverwaltung-osx-arm64.tar.gz", istBundle: true));
        }

        foreach (var problem in Enum.GetValues<StorageProblem>())
        {
            daten.Add($"Sicherungsfehler/{problem}", Bandtexte.Sicherungsfehler(problem));
        }

        daten.Add("ErzeugteBuchungen/1", Bandtexte.ErzeugteBuchungen(["07.08.2026 - 42,90 €"]));
        daten.Add("ErzeugteBuchungen/2", Bandtexte.ErzeugteBuchungen(
            ["07.08.2026 - 42,90 €", "08.08.2026 - 12,00 € (Zeitung)"]));

        return daten;
    }

    [Theory]
    [MemberData(nameof(AlleBaender))]
    public void Der_Titel_ist_eine_Zeile_und_kein_Satz(string name, Bandmeldung band)
    {
        Assert.False(string.IsNullOrWhiteSpace(band.Titel), $"„{name}“ hat keinen Titel.");

        Assert.DoesNotContain('\n', band.Titel);

        Assert.True(
            band.Titel.Length <= TitelHoechstlaenge,
            $"Der Titel von „{name}“ ist {band.Titel.Length} Zeichen lang und passt "
            + $"damit nicht mehr in eine Zeile: „{band.Titel}“");

        // Kein Schlusspunkt: eine Ueberschrift ist kein Satz. Ein
        // Fragezeichen waere eines - deshalb wird nur der Punkt verboten.
        Assert.False(
            band.Titel.EndsWith('.'),
            $"Der Titel von „{name}“ endet auf einen Punkt: „{band.Titel}“");
    }

    [Theory]
    [MemberData(nameof(AlleBaender))]
    public void Die_Kurzzeile_bleibt_kurz_und_einzeilig(string name, Bandmeldung band)
    {
        Assert.False(string.IsNullOrWhiteSpace(band.Kurz), $"„{name}“ hat keine Kurzzeile.");

        // Kein eigener Zeilenumbruch: umgebrochen wird im Band, nach der
        // tatsaechlichen Breite. Ein fest eingebauter Umbruch sitzt genau
        // bei einer einzigen Fenstergroesse richtig.
        Assert.DoesNotContain('\n', band.Kurz);

        Assert.True(
            band.Kurz.Length <= KurzHoechstlaenge,
            $"Die Kurzzeile von „{name}“ ist {band.Kurz.Length} Zeichen lang — "
            + $"das sind mehr als zwei Zeilen im Band: „{band.Kurz}“");
    }

    // Der Kern der Abmachung mit Regel 12: das Band darf kuerzer sein als
    // die Sache, ABER die vollstaendige Erklaerung muss einen Klick
    // entfernt bereitliegen. Eine Kurzfassung ohne Langfassung waere
    // genau die Verschlechterung, die dieser Umbau nicht sein soll.
    [Theory]
    [MemberData(nameof(AlleBaender))]
    public void Jedes_Band_hat_seinen_langen_Text_hinter_Details(string name, Bandmeldung band)
    {
        Assert.True(band.HatDetails, $"„{name}“ hat keine Details — die Erklärung fehlt damit ganz.");

        Assert.True(
            band.Details!.Length > band.Kurz.Length,
            $"Die Details von „{name}“ sagen nicht mehr als die Kurzzeile.");
    }

    [Theory]
    [MemberData(nameof(AlleBaender))]
    public void Vollstaendig_enthaelt_alle_drei_Teile(string name, Bandmeldung band)
    {
        Assert.Contains(band.Titel, band.Vollstaendig);
        Assert.Contains(band.Kurz, band.Vollstaendig);
        Assert.Contains(band.Details!, band.Vollstaendig);
        Assert.NotEqual(string.Empty, name);
    }

    // Die Rangfolge ist keine Kosmetik: sie entscheidet, welches Band bei
    // mehreren zuerst sichtbar ist. Ein Fehler darf nie hinter einem
    // Hinweis verschwinden.
    [Fact]
    public void Ein_Sicherungsfehler_wiegt_schwerer_als_erzeugte_Buchungen()
    {
        Assert.True(
            Bandtexte.Sicherungsfehler(StorageProblem.DiskFull).Rang
            > Bandtexte.ErzeugteBuchungen(["x"]).Rang);
    }
}
