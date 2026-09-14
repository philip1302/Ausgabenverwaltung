using Ausgabenverwaltung.Core.YearInReview;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Saetze des Jahresrueckblicks. Der wichtigste Test hier ist der
/// Waechter gegen das Minuszeichen: die Auswertung rechnet mit negativen
/// Ausgabenbetraegen, und ein einziger durchgereichter Wert wuerde aus
/// "1.240,00 € mehr" ein "-1.240,00 €" machen - richtig gerechnet und
/// trotzdem unverstaendlich.
/// </summary>
public class JahresrueckblickTextTests
{
    private static readonly ReviewComparisonPeriods Zeitraeume =
        ReviewPeriods.Build(2026, ReviewSpan.GanzeKalenderjahre, new DateOnly(2026, 9, 13));

    private static ReviewFinding Befund(
        ReviewFindingKind art,
        long vorjahr = 174_000,
        long jahr = 298_000,
        int vorjahrAnzahl = 12,
        int jahrAnzahl = 14,
        string gegenstand = "Wohnen › Nebenkosten") =>
        new()
        {
            Kind = art,
            CategoryId = art is ReviewFindingKind.TeuersterMonat
                                or ReviewFindingKind.GuenstigsterMonat ? null : 1,
            Subject = gegenstand,
            PeriodKey = null,
            PreviousCents = vorjahr,
            CurrentCents = jahr,
            DeltaCents = jahr - vorjahr,
            RelativeChange = ReviewMath.RelativeChange(vorjahr, jahr),
            Share = 0.18m,
            PreviousCount = vorjahrAnzahl,
            CurrentCount = jahrAnzahl,
            Score = 1,
        };

    private static IEnumerable<ReviewFindingKind> AlleArten =>
        Enum.GetValues<ReviewFindingKind>();

    // ================= Richtung im Wort, nicht im Vorzeichen =================

    [Fact]
    public void Ein_Mehrverbrauch_heisst_mehr()
    {
        Assert.Equal("412,00 € mehr", Entschuetze(ReviewText.Veraenderung(41_200)));
    }

    [Fact]
    public void Ein_Rueckgang_heisst_weniger()
    {
        Assert.Equal("88,00 € weniger", Entschuetze(ReviewText.Veraenderung(-8_800)));
    }

    [Fact]
    public void Ohne_Unterschied_heisst_es_unveraendert()
    {
        Assert.Equal("unverändert", ReviewText.Veraenderung(0));
    }

    /// <summary>
    /// Der Waechter. Geprueft wird ueber ALLE Befundarten und in beide
    /// Richtungen - ein Minuszeichen unmittelbar vor einer Ziffer darf in
    /// keinem Satz stehen.
    /// </summary>
    [Theory]
    [MemberData(nameof(ArtenUndRichtungen))]
    public void Kein_Befundsatz_stellt_einem_Betrag_ein_Minuszeichen_voran(
        ReviewFindingKind art, bool gestiegen)
    {
        var satz = ReviewText.Satz(gestiegen
            ? Befund(art, vorjahr: 174_000, jahr: 298_000)
            : Befund(art, vorjahr: 298_000, jahr: 174_000, vorjahrAnzahl: 20, jahrAnzahl: 9));

        Assert.DoesNotContain("-1", satz, StringComparison.Ordinal);
        Assert.DoesNotContain("-2", satz, StringComparison.Ordinal);
        Assert.DoesNotContain("-3", satz, StringComparison.Ordinal);
        Assert.DoesNotContain("−", satz, StringComparison.Ordinal);
    }

    public static TheoryData<ReviewFindingKind, bool> ArtenUndRichtungen()
    {
        var daten = new TheoryData<ReviewFindingKind, bool>();
        foreach (var art in AlleArten)
        {
            daten.Add(art, true);
            daten.Add(art, false);
        }

        return daten;
    }

    // ================= Vollstaendigkeit =================

    [Fact]
    public void Jede_Befundart_hat_eine_Ueberschrift()
    {
        foreach (var art in AlleArten)
        {
            var ueberschrift = ReviewText.Ueberschrift(art);

            Assert.False(string.IsNullOrWhiteSpace(ueberschrift));
            // Kein durchgereichter Aufzaehlungsname auf dem Bildschirm.
            Assert.NotEqual(art.ToString(), ueberschrift);
        }
    }

    [Fact]
    public void Jeder_Befundsatz_nennt_seinen_Gegenstand_und_einen_Betrag()
    {
        foreach (var art in AlleArten)
        {
            var satz = ReviewText.Satz(Befund(art, gegenstand: "Wohnen › Strom"));

            Assert.Contains("Wohnen › Strom", satz, StringComparison.Ordinal);
            Assert.Contains("€", satz, StringComparison.Ordinal);
            Assert.EndsWith(".", satz, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Jede_Kennzahl_hat_eine_Beschriftung()
    {
        foreach (var art in Enum.GetValues<ReviewMetricKind>())
        {
            Assert.False(string.IsNullOrWhiteSpace(ReviewText.KennzahlName(art)));
        }
    }

    // ================= Das Fenster am Fragezeichen =================

    [Fact]
    public void Jede_Befundart_sagt_wonach_gesucht_wurde()
    {
        foreach (var art in AlleArten)
        {
            var bedeutung = ReviewText.Bedeutung(art);

            Assert.False(string.IsNullOrWhiteSpace(bedeutung));
            Assert.NotEqual(art.ToString(), bedeutung);
            Assert.EndsWith(".", bedeutung, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Eine_Kategoriekarte_stellt_die_beiden_Jahre_mit_ihren_Jahreszahlen_gegenueber()
    {
        var erklaerung = ReviewText.Erklaerung(
            Befund(ReviewFindingKind.StaerksteSteigerung), Zeitraeume);

        Assert.Equal("2026", erklaerung.Werte[0].Beschriftung);
        Assert.Equal("2025", erklaerung.Werte[1].Beschriftung);
        Assert.Equal("2.980,00 €", Entschuetze(erklaerung.Werte[0].Wert));
        Assert.Equal("1.740,00 €", Entschuetze(erklaerung.Werte[1].Wert));
        Assert.Equal("1.240,00 € mehr", Entschuetze(erklaerung.Werte[2].Wert));
    }

    /// <summary>
    /// Ein Monatsbefund vergleicht den Monat mit dem MONATSSCHNITT und
    /// nicht mit dem Vorjahr (siehe ReviewFindings.BaueMonat). Stuende
    /// ueber dem Schnitt eine Jahreszahl, waere die Zahl darunter falsch
    /// beschriftet - und niemand haette eine Chance, das zu merken.
    /// </summary>
    [Fact]
    public void Eine_Monatskarte_vergleicht_mit_dem_Monatsschnitt_und_nennt_kein_Jahr()
    {
        var erklaerung = ReviewText.Erklaerung(
            Befund(ReviewFindingKind.TeuersterMonat, gegenstand: "Juni 2026"), Zeitraeume);

        var beschriftungen = erklaerung.Werte.Select(wert => wert.Beschriftung).ToList();

        Assert.Contains("Dein Monatsschnitt", beschriftungen);
        Assert.DoesNotContain("2026", beschriftungen);
        Assert.DoesNotContain("2025", beschriftungen);
    }

    [Fact]
    public void Ein_weggefallener_Posten_sagt_keine_Buchung_statt_null_Euro()
    {
        var weg = Befund(
            ReviewFindingKind.Weggefallen, vorjahr: 90_000, jahr: 0,
            vorjahrAnzahl: 4, jahrAnzahl: 0);

        var erklaerung = ReviewText.Erklaerung(weg, Zeitraeume);

        Assert.Equal("keine Buchung", erklaerung.Werte[0].Wert);
        Assert.DoesNotContain(
            erklaerung.Werte, wert => wert.Beschriftung == "Im Schnitt je Buchung");
    }

    /// <summary>Bei genau einer Buchung waere der Schnitt derselbe Betrag
    /// noch einmal - eine Zeile, die nichts hinzufuegt.</summary>
    [Fact]
    public void Eine_einzige_Buchung_bekommt_keinen_Durchschnitt()
    {
        var einmalig = Befund(
            ReviewFindingKind.StaerksteSteigerung, vorjahrAnzahl: 1, jahrAnzahl: 1);

        Assert.DoesNotContain(
            ReviewText.Erklaerung(einmalig, Zeitraeume).Werte,
            wert => wert.Beschriftung == "Im Schnitt je Buchung");
    }

    /// <summary>
    /// Derselbe Waechter wie fuer die Saetze, jetzt fuer die Zahlen im
    /// Fenster: auch dort darf kein Minuszeichen vor einer Ziffer stehen.
    /// </summary>
    [Theory]
    [MemberData(nameof(ArtenUndRichtungen))]
    public void Kein_Wert_im_Erklaerfenster_traegt_ein_Minuszeichen(
        ReviewFindingKind art, bool gestiegen)
    {
        var erklaerung = ReviewText.Erklaerung(gestiegen
            ? Befund(art, vorjahr: 174_000, jahr: 298_000)
            : Befund(art, vorjahr: 298_000, jahr: 174_000, vorjahrAnzahl: 20, jahrAnzahl: 9),
            Zeitraeume);

        foreach (var wert in erklaerung.Werte)
        {
            Assert.False(string.IsNullOrWhiteSpace(wert.Beschriftung));
            Assert.DoesNotContain("-1", wert.Wert, StringComparison.Ordinal);
            Assert.DoesNotContain("-2", wert.Wert, StringComparison.Ordinal);
            Assert.DoesNotContain("-3", wert.Wert, StringComparison.Ordinal);
            Assert.DoesNotContain("−", wert.Wert, StringComparison.Ordinal);
        }
    }

    // ================= Prozentzahlen =================

    [Fact]
    public void Ohne_Vorjahreswert_steht_neu_statt_einer_Prozentzahl()
    {
        Assert.Equal("neu", ReviewText.ProzentText(null));
    }

    [Fact]
    public void Ein_vollstaendiger_Wegfall_heisst_nicht_mehr_gebucht()
    {
        Assert.Equal("nicht mehr gebucht", ReviewText.ProzentText(-1m));
    }

    [Fact]
    public void Eine_Steigerung_wird_als_Prozent_mehr_ausgedrueckt()
    {
        Assert.Equal("34 % mehr", Entschuetze(ReviewText.ProzentText(0.34m)));
    }

    [Fact]
    public void Ein_Rueckgang_wird_als_Prozent_weniger_ausgedrueckt()
    {
        Assert.Equal("43 % weniger", Entschuetze(ReviewText.ProzentText(-0.43m)));
    }

    [Fact]
    public void Ein_Anteil_traegt_keine_Richtung()
    {
        Assert.Equal("18 %", Entschuetze(ReviewText.AnteilText(0.18m)));
    }

    // ================= Zeitraum und Lage =================

    [Fact]
    public void Der_Zeitraumhinweis_nennt_beide_Jahre_und_die_Grenzen()
    {
        var teilweise = ReviewPeriods.Build(
            2026, ReviewSpan.GleicherZeitraum, new DateOnly(2026, 9, 13));

        var hinweis = ReviewText.ZeitraumHinweis(teilweise);

        Assert.Contains("2026", hinweis, StringComparison.Ordinal);
        Assert.Contains("2025", hinweis, StringComparison.Ordinal);
        Assert.Contains("1. Januar bis 13. September", hinweis, StringComparison.Ordinal);
    }

    /// <summary>
    /// Wer im September ganze Kalenderjahre vergleicht, sieht ueberall
    /// einen Rueckgang, der keiner ist. Das muss dabeistehen.
    /// </summary>
    [Fact]
    public void Ganze_Kalenderjahre_im_laufenden_Jahr_werden_ausdruecklich_eingeordnet()
    {
        var heute = new DateOnly(2026, 9, 13);
        var ganz = ReviewPeriods.Build(2026, ReviewSpan.GanzeKalenderjahre, heute);

        Assert.NotEqual(string.Empty, ReviewText.GanzjahresWarnung(ganz, heute));
    }

    [Fact]
    public void Ein_abgeschlossenes_Jahr_braucht_keine_Warnung()
    {
        var heute = new DateOnly(2026, 9, 13);
        var abgeschlossen = ReviewPeriods.Build(2024, ReviewSpan.GanzeKalenderjahre, heute);

        Assert.Equal(string.Empty, ReviewText.GanzjahresWarnung(abgeschlossen, heute));
    }

    [Theory]
    [InlineData(ReviewDataState.NochNichtsErfasst)]
    [InlineData(ReviewDataState.VorjahrOhneBuchung)]
    [InlineData(ReviewDataState.KeineAuffaelligkeiten)]
    public void Jede_Lage_ohne_Befunde_hat_einen_Satz(ReviewDataState lage)
    {
        Assert.False(string.IsNullOrWhiteSpace(ReviewText.LageHinweis(lage, Zeitraeume)));
    }

    [Fact]
    public void Gibt_es_etwas_zu_erzaehlen_steht_kein_Lagesatz_im_Weg()
    {
        Assert.Equal(
            string.Empty, ReviewText.LageHinweis(ReviewDataState.Vollstaendig, Zeitraeume));
    }

    // Das geschuetzte Leerzeichen aus EuroText durch ein gewoehnliches
    // ersetzen, damit die Erwartung im Test lesbar bleibt.
    private static string Entschuetze(string text) => text.Replace(' ', ' ');
}
