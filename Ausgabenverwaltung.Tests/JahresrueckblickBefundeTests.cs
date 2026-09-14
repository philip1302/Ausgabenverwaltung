using Ausgabenverwaltung.Core.YearInReview;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Auswahl und Rangfolge der Befunde - eine reine Funktion, deshalb
/// ohne Datenbank und mit von Hand gebauten Baeumen.
///
/// Hier liegt der eigentliche Wert des Jahresrueckblicks: dass er die
/// wenigen Punkte findet, die etwas bedeuten, und das viele uebergeht, das
/// nur gross aussieht. Die drei Gefahren werden einzeln geprueft -
/// Rauschen, Doppelmeldung und die fehlende Grundlage fuer eine
/// Prozentzahl.
/// </summary>
public class JahresrueckblickBefundeTests
{
    private static readonly ReviewComparisonPeriods Zeitraeume =
        ReviewPeriods.Build(2026, ReviewSpan.GanzeKalenderjahre, new DateOnly(2026, 9, 13));

    private static ReviewCategoryChange Knoten(
        int id,
        string name,
        long vorjahr,
        long jahr,
        int vorjahrAnzahl = 2,
        int jahrAnzahl = 2,
        bool archiviert = false,
        int tiefe = 0,
        IReadOnlyList<ReviewCategoryChange>? kinder = null) =>
        new()
        {
            CategoryId = id,
            Name = name,
            FullPath = name,
            Depth = tiefe,
            IsArchived = archiviert,
            PreviousCents = vorjahr,
            CurrentCents = jahr,
            // Ein Betrag von null ohne Buchung heisst "gab es nicht" - das
            // unterscheidet "neu" von "hebt sich auf".
            PreviousCount = vorjahr == 0 && vorjahrAnzahl == 2 ? 0 : vorjahrAnzahl,
            CurrentCount = jahr == 0 && jahrAnzahl == 2 ? 0 : jahrAnzahl,
            Children = kinder ?? Array.Empty<ReviewCategoryChange>(),
        };

    private static ReviewComparison Vergleich(params ReviewCategoryChange[] wurzeln) =>
        new(
            Zeitraeume,
            wurzeln,
            PreviousTotalCents: wurzeln.Sum(w => w.PreviousCents),
            CurrentTotalCents: wurzeln.Sum(w => w.CurrentCents),
            PreviousTotalCount: wurzeln.Sum(w => w.PreviousCount),
            CurrentTotalCount: wurzeln.Sum(w => w.CurrentCount));

    // Zwoelf gleich hohe Monate - ohne Ausschlag, damit die Monatsbefunde
    // die Kategoriebefunde in den Ranglisten-Tests nicht stoeren.
    private static IReadOnlyList<ReviewMonth> RuhigeMonate(long jahresSumme)
    {
        var proMonat = jahresSumme / 12;
        return Enumerable.Range(1, 12)
            .Select(m => new ReviewMonth($"2026-{m:D2}", $"Monat {m}", proMonat, 1))
            .ToList();
    }

    private static IReadOnlyList<ReviewFinding> Befunde(
        ReviewComparison vergleich, IReadOnlyList<ReviewMonth>? monate = null) =>
        ReviewFindings.Build(
            vergleich, monate ?? RuhigeMonate(vergleich.CurrentTotalCents));

    private static ReviewFinding? VonArt(
        IReadOnlyList<ReviewFinding> befunde, ReviewFindingKind art) =>
        befunde.FirstOrDefault(b => b.Kind == art);

    // ================= Entrauschung =================

    /// <summary>
    /// Der Kernfall. Zwei Euro auf zwanzig sind plus 900 Prozent und
    /// wuerden ohne Schwellen jede Rangliste anfuehren - dabei ist es kein
    /// Jahresereignis, sondern ein einzelner Kauf.
    /// </summary>
    [Fact]
    public void Eine_Steigerung_von_zwei_auf_zwanzig_Euro_wird_nicht_gemeldet()
    {
        var vergleich = Vergleich(
            Knoten(1, "Kleinkram", 200, 2_000),
            Knoten(2, "Miete", 1_000_000, 1_000_000));

        Assert.DoesNotContain(Befunde(vergleich), b => b.CategoryId == 1);
    }

    /// <summary>
    /// Die andere Richtung: ein grosser Betrag allein genuegt nicht. 60
    /// Euro mehr auf 10.000 Euro Miete sind Streuung im Grossen.
    /// </summary>
    [Fact]
    public void Ein_Prozent_mehr_bei_der_Miete_ist_kein_Befund()
    {
        var vergleich = Vergleich(Knoten(1, "Miete", 1_000_000, 1_006_000));

        Assert.Null(VonArt(Befunde(vergleich), ReviewFindingKind.StaerksteSteigerung));
    }

    /// <summary>
    /// Der absolute Betrag bleibt der Hauptmassstab, aber bei gleich
    /// grossem Unterschied entscheidet das Verhaeltnis: 200 Euro mehr auf
    /// 200 Euro Grundlage wiegen schwerer als auf 2.000.
    /// </summary>
    [Fact]
    public void Dieselbe_Zunahme_auf_kleiner_Grundlage_steht_vor_der_auf_grosser()
    {
        var vergleich = Vergleich(
            Knoten(1, "Klein", 20_000, 40_000),
            Knoten(2, "Gross", 200_000, 220_000));

        var steigerung = VonArt(Befunde(vergleich), ReviewFindingKind.StaerksteSteigerung);

        Assert.NotNull(steigerung);
        Assert.Equal(1, steigerung!.CategoryId);
    }

    /// <summary>
    /// Ohne Deckel gewaenne eine Kategorie mit winzigem Vorjahreswert
    /// jeden Vergleich allein durch ihr Verhaeltnis.
    /// </summary>
    [Fact]
    public void Die_Punktzahl_ist_bei_extremen_Verhaeltnissen_gedeckelt()
    {
        var vervierfacht = ReviewMath.Score(10_000, 50_000);
        var verzwanzigfacht = ReviewMath.Score(2_000, 42_000);

        Assert.Equal(vervierfacht, verzwanzigfacht);
    }

    [Fact]
    public void Knapp_unter_der_Schwelle_ergibt_nichts_und_knapp_darueber_einen_Befund()
    {
        // Die Miete steht nur dabei, damit die Datenlage dick genug ist -
        // sie selbst veraendert sich nicht.
        var darunter = Vergleich(
            Knoten(1, "Strom", 40_000, 44_999), Knoten(2, "Miete", 500_000, 500_000));
        var darueber = Vergleich(
            Knoten(1, "Strom", 40_000, 45_000), Knoten(2, "Miete", 500_000, 500_000));

        Assert.Null(VonArt(Befunde(darunter), ReviewFindingKind.StaerksteSteigerung));
        Assert.NotNull(VonArt(Befunde(darueber), ReviewFindingKind.StaerksteSteigerung));
    }

    [Fact]
    public void Bei_zu_duenner_Datenlage_meldet_der_Rueckblick_gar_nichts()
    {
        var vergleich = Vergleich(Knoten(1, "Kiosk", 12_000, 30_000));

        Assert.Empty(Befunde(vergleich));
    }

    // ================= Doppelmeldung =================

    /// <summary>
    /// Die Auswertung summiert astweise auf - derselbe Zuwachs steht in
    /// jeder Zeile daruber. Gemeldet wird die genaueste Stelle, die ihn
    /// erklaert.
    /// </summary>
    [Fact]
    public void Traegt_ein_Kind_den_Loewenanteil_meldet_der_Rueckblick_das_Kind()
    {
        var strom = Knoten(2, "Strom", 20_000, 58_000, tiefe: 1);
        var wohnen = Knoten(1, "Wohnen", 100_000, 140_000, kinder: new[] { strom });

        var steigerung = VonArt(
            Befunde(Vergleich(wohnen)), ReviewFindingKind.StaerksteSteigerung);

        Assert.NotNull(steigerung);
        Assert.Equal(2, steigerung!.CategoryId);
    }

    /// <summary>
    /// Verteilt sich die Veraenderung auf mehrere Zweige, ist sie nur eine
    /// Stufe hoeher eine Aussage - kein einzelnes Kind erklaert sie.
    /// </summary>
    [Fact]
    public void Verteilt_sich_die_Veraenderung_auf_zwei_Zweige_meldet_der_Rueckblick_die_Oberkategorie()
    {
        var a = Knoten(2, "Nebenkosten", 50_000, 70_000, tiefe: 1);
        var b = Knoten(3, "Versicherung", 50_000, 70_000, tiefe: 1);
        var wohnen = Knoten(1, "Wohnen", 100_000, 140_000, kinder: new[] { a, b });

        var steigerung = VonArt(
            Befunde(Vergleich(wohnen)), ReviewFindingKind.StaerksteSteigerung);

        Assert.NotNull(steigerung);
        Assert.Equal(1, steigerung!.CategoryId);
    }

    /// <summary>
    /// Dass anderswo gespart wurde, erklaert keinen Zuwachs. Ohne die
    /// Vorzeichenbedingung waere die Oberkategorie hier verdraengt worden -
    /// das Kind ist mit 300 Euro gross genug dafuer.
    /// </summary>
    [Fact]
    public void Ein_gegenlaeufiges_Kind_verdraengt_die_Oberkategorie_nicht()
    {
        var gespart = Knoten(2, "Reisen", 50_000, 20_000, tiefe: 1);
        var wohnen = Knoten(1, "Wohnen", 100_000, 140_000, kinder: new[] { gespart });

        var steigerung = VonArt(
            Befunde(Vergleich(wohnen)), ReviewFindingKind.StaerksteSteigerung);

        Assert.NotNull(steigerung);
        Assert.Equal(1, steigerung!.CategoryId);
    }

    [Fact]
    public void Hat_die_Oberkategorie_nur_ein_belegtes_Kind_meldet_der_Rueckblick_das_Kind()
    {
        var strom = Knoten(2, "Strom", 20_000, 80_000, tiefe: 1);
        var wohnen = Knoten(1, "Wohnen", 20_000, 80_000, kinder: new[] { strom });

        var steigerung = VonArt(
            Befunde(Vergleich(wohnen)), ReviewFindingKind.StaerksteSteigerung);

        Assert.NotNull(steigerung);
        Assert.Equal(2, steigerung!.CategoryId);
    }

    /// <summary>
    /// Dieselbe VERSCHIEBUNG steht nur einmal da - nicht einmal grob in
    /// der Oberkategorie und einmal genau im Kind. Dass die Oberkategorie
    /// daneben noch als groesster Kostenblock erscheinen darf, ist Absicht:
    /// das ist eine andere Aussage.
    /// </summary>
    [Fact]
    public void Dieselbe_Verschiebung_wird_nur_auf_einer_Stufe_gemeldet()
    {
        var strom = Knoten(2, "Strom", 20_000, 400_000, tiefe: 1);
        var wohnen = Knoten(1, "Wohnen", 100_000, 480_000, kinder: new[] { strom });

        var veraenderungen = Befunde(Vergleich(wohnen))
            .Where(b => b.Kind is ReviewFindingKind.StaerksteSteigerung
                                or ReviewFindingKind.StaerksterRueckgang)
            .ToList();

        Assert.DoesNotContain(veraenderungen, b => b.CategoryId == 1);
        Assert.Contains(veraenderungen, b => b.CategoryId == 2);
    }

    /// <summary>
    /// Eine Veraenderung ist die bessere Nachricht als eine Tatsache, die
    /// letztes Jahr auch schon galt. Ohne den Vorrang wuerde "das ist dein
    /// groesster Kostenblock" die Steigerung derselben Kategorie
    /// verdraengen, sobald die Summe groesser ist als der Unterschied.
    /// </summary>
    [Fact]
    public void Eine_Veraenderung_geht_einem_Dauerzustand_vor()
    {
        var befunde = Befunde(Vergleich(Knoten(1, "Wohnen", 100_000, 140_000)));

        Assert.Contains(befunde, b => b.Kind == ReviewFindingKind.StaerksteSteigerung);
        Assert.DoesNotContain(befunde, b => b.Kind == ReviewFindingKind.GroessterKostenblock);
    }

    [Fact]
    public void Eine_Kategorie_erscheint_hoechstens_in_einem_Befund()
    {
        var vergleich = Vergleich(
            Knoten(1, "Wohnen", 100_000, 300_000),
            Knoten(2, "Neu", 0, 200_000),
            Knoten(3, "Weg", 150_000, 0),
            Knoten(4, "Kiosk", 50_000, 60_000, vorjahrAnzahl: 40, jahrAnzahl: 45));

        var befunde = Befunde(vergleich);
        var kategorien = befunde.Where(b => b.CategoryId is not null).Select(b => b.CategoryId);

        Assert.Equal(kategorien.Distinct().Count(), kategorien.Count());
    }

    // ================= Keine Grundlage fuer eine Prozentzahl =================

    [Fact]
    public void Ein_Vorjahr_von_null_wird_als_neu_gemeldet_und_nicht_als_unendliche_Prozentzahl()
    {
        var befund = VonArt(
            Befunde(Vergleich(Knoten(1, "Leasing", 0, 240_000))),
            ReviewFindingKind.NeuHinzugekommen);

        Assert.NotNull(befund);
        Assert.Null(befund!.RelativeChange);
        Assert.Equal(240_000, befund.CurrentCents);
    }

    [Fact]
    public void Faellt_eine_Kategorie_weg_sind_das_genau_minus_hundert_Prozent()
    {
        var befund = VonArt(
            Befunde(Vergleich(
                Knoten(1, "Fitness", 36_000, 0),
                Knoten(2, "Miete", 500_000, 500_000))),
            ReviewFindingKind.Weggefallen);

        Assert.NotNull(befund);
        Assert.Equal(-1m, befund!.RelativeChange);
    }

    [Fact]
    public void Null_gegen_null_ergibt_keinen_Befund()
    {
        var vergleich = Vergleich(
            Knoten(1, "Nichts", 0, 0),
            Knoten(2, "Miete", 500_000, 500_000));

        Assert.DoesNotContain(Befunde(vergleich), b => b.CategoryId == 1);
    }

    // ================= Archiv =================

    /// <summary>
    /// Eine archivierte Kategorie ohne Buchungen im laufenden Jahr kann
    /// nicht "gestiegen" oder "der groesste Kostenblock" sein - sie wird
    /// gar nicht mehr bebucht. Das folgt aus den Schwellen und braucht
    /// keine eigene Regel; dieser Test haelt es fest.
    /// </summary>
    [Fact]
    public void Eine_archivierte_Kategorie_ohne_Buchung_erscheint_nur_als_weggefallen()
    {
        var vergleich = Vergleich(
            Knoten(1, "Fitness", 120_000, 0, archiviert: true),
            Knoten(2, "Miete", 500_000, 500_000));

        var ihre = Befunde(vergleich).Where(b => b.CategoryId == 1).ToList();

        Assert.All(ihre, b => Assert.Equal(ReviewFindingKind.Weggefallen, b.Kind));
    }

    [Fact]
    public void Eine_archivierte_Kategorie_mit_Buchungen_wird_normal_bewertet()
    {
        var vergleich = Vergleich(
            Knoten(1, "Fitness", 40_000, 120_000, archiviert: true));

        var steigerung = VonArt(Befunde(vergleich), ReviewFindingKind.StaerksteSteigerung);

        Assert.NotNull(steigerung);
        Assert.Equal(1, steigerung!.CategoryId);
    }

    // ================= Monate und Anzahl =================

    [Fact]
    public void Der_teuerste_Monat_wird_benannt()
    {
        var monate = Enumerable.Range(1, 12)
            .Select(m => new ReviewMonth(
                $"2026-{m:D2}", $"Monat {m}", m == 7 ? 300_000 : 50_000, 3))
            .ToList();

        var befund = VonArt(
            Befunde(Vergleich(Knoten(1, "Alles", 800_000, 850_000)), monate),
            ReviewFindingKind.TeuersterMonat);

        Assert.NotNull(befund);
        Assert.Equal("2026-07", befund!.PeriodKey);
    }

    /// <summary>
    /// Der guenstigste Monat misst sich am eigenen Monatsschnitt und nicht
    /// am Vorjahresmonat - "im Februar war es ruhig" ist eine Aussage ueber
    /// dieses Jahr.
    /// </summary>
    [Fact]
    public void Der_guenstigste_Monat_misst_sich_am_eigenen_Durchschnitt()
    {
        var monate = Enumerable.Range(1, 12)
            .Select(m => new ReviewMonth(
                $"2026-{m:D2}", $"Monat {m}", m == 2 ? 0 : 100_000, m == 2 ? 0 : 4))
            .ToList();

        var befund = VonArt(
            Befunde(Vergleich(Knoten(1, "Alles", 1_000_000, 1_100_000)), monate),
            ReviewFindingKind.GuenstigsterMonat);

        Assert.NotNull(befund);
        Assert.Equal("2026-02", befund!.PeriodKey);
        Assert.True(befund.DeltaCents < 0);
    }

    [Fact]
    public void Der_teuerste_und_der_guenstigste_Monat_sind_nie_derselbe()
    {
        var monate = Enumerable.Range(1, 12)
            .Select(m => new ReviewMonth($"2026-{m:D2}", $"Monat {m}", 100_000, 4))
            .ToList();

        var befunde = Befunde(Vergleich(Knoten(1, "Alles", 1_100_000, 1_200_000)), monate);
        var monatsbefunde = befunde.Where(b => b.PeriodKey is not null).ToList();

        Assert.Equal(
            monatsbefunde.Select(b => b.PeriodKey).Distinct().Count(), monatsbefunde.Count);
    }

    /// <summary>
    /// Die eine Frage, die eine reine Summenbetrachtung nie beantwortet:
    /// wo geht das Geld in kleinen Schritten weg.
    /// </summary>
    [Fact]
    public void Viele_kleine_Buchungen_ergeben_einen_eigenen_Befund()
    {
        var vergleich = Vergleich(
            Knoten(1, "Kiosk", 45_000, 45_000, vorjahrAnzahl: 50, jahrAnzahl: 60),
            Knoten(2, "Miete", 500_000, 500_000));

        var befund = VonArt(Befunde(vergleich), ReviewFindingKind.VieleKleineBuchungen);

        Assert.NotNull(befund);
        Assert.Equal(60, befund!.CurrentCount);
    }

    /// <summary>
    /// Anzahlgetriebene Befunde sehen nur Blaetter an: die Anzahl einer
    /// Oberkategorie ist die Summe ihres Astes und beantwortet die Frage
    /// "wo buchst du staendig" nicht.
    /// </summary>
    [Fact]
    public void Die_haeufigste_Kategorie_ist_immer_ein_Blatt()
    {
        // Der Schnitt liegt ueber 15 Euro, damit hier wirklich die
        // Haeufigkeit geprueft wird und nicht das Kleinvieh.
        var kiosk = Knoten(
            2, "Kiosk", 100_000, 100_000, vorjahrAnzahl: 40, jahrAnzahl: 44, tiefe: 1);
        var alltag = Knoten(
            1, "Alltag", 200_000, 200_000, vorjahrAnzahl: 60, jahrAnzahl: 64,
            kinder: new[] { kiosk });

        var befund = VonArt(Befunde(Vergleich(alltag)), ReviewFindingKind.HaeufigsteKategorie);

        Assert.NotNull(befund);
        Assert.Equal(2, befund!.CategoryId);
    }

    // ================= Rangfolge und Lage =================

    [Fact]
    public void Die_Befunde_stehen_nach_Punktzahl_absteigend()
    {
        var vergleich = Vergleich(
            Knoten(1, "Wohnen", 100_000, 300_000),
            Knoten(2, "Neu", 0, 200_000),
            Knoten(3, "Weg", 150_000, 0));

        var befunde = Befunde(vergleich);

        Assert.Equal(befunde.OrderByDescending(b => b.Score).Select(b => b.Kind),
            befunde.Select(b => b.Kind));
    }

    [Fact]
    public void Bei_gleichen_Eingaben_bleibt_die_Reihenfolge_gleich()
    {
        var vergleich = Vergleich(
            Knoten(1, "A", 100_000, 200_000),
            Knoten(2, "B", 100_000, 200_000),
            Knoten(3, "C", 100_000, 200_000));

        Assert.Equal(
            Befunde(vergleich).Select(b => b.Subject),
            Befunde(vergleich).Select(b => b.Subject));
    }

    [Fact]
    public void Es_stehen_hoechstens_sechs_Befunde_da()
    {
        var wurzeln = Enumerable.Range(1, 20)
            .Select(i => Knoten(i, "K" + i, 100_000, 100_000 + i * 20_000,
                vorjahrAnzahl: 30, jahrAnzahl: 40))
            .ToArray();

        Assert.True(Befunde(Vergleich(wurzeln)).Count <= ReviewThresholds.HoechsteBefundzahl);
    }

    [Fact]
    public void Jede_Befundart_kommt_hoechstens_einmal_vor()
    {
        var wurzeln = Enumerable.Range(1, 20)
            .Select(i => Knoten(i, "K" + i, 100_000, 100_000 + i * 20_000))
            .ToArray();

        var arten = Befunde(Vergleich(wurzeln)).Select(b => b.Kind).ToList();

        Assert.Equal(arten.Distinct().Count(), arten.Count);
    }

    /// <summary>
    /// Dass sich wenig verschoben hat, ist ein Ergebnis und kein Mangel.
    /// Wer hier "Noch nichts erfasst" liest, sucht einen Fehler, den es
    /// nicht gibt.
    /// </summary>
    [Fact]
    public void Bei_ruhigen_Zahlen_steht_ein_Ergebnis_und_kein_Fehler()
    {
        var vergleich = Vergleich(Knoten(1, "Alltag", 28_000, 30_000));
        var befunde = Befunde(vergleich);

        Assert.Empty(befunde);
        Assert.Equal(
            ReviewDataState.KeineAuffaelligkeiten,
            ReviewFindings.Bewerte(vergleich, befunde, hatIrgendeineBuchung: true));
    }

    [Fact]
    public void Ohne_jede_Buchung_fuehrt_die_Lage_zum_Erfassungsangebot()
    {
        var vergleich = Vergleich();

        Assert.Equal(
            ReviewDataState.NochNichtsErfasst,
            ReviewFindings.Bewerte(vergleich, Array.Empty<ReviewFinding>(),
                hatIrgendeineBuchung: false));
    }

    [Fact]
    public void Ohne_Vorjahresbuchung_meldet_die_Lage_das_fehlende_Vergleichsjahr()
    {
        var vergleich = Vergleich(Knoten(1, "Neu", 0, 200_000));

        Assert.Equal(
            ReviewDataState.VorjahrOhneBuchung,
            ReviewFindings.Bewerte(vergleich, Befunde(vergleich), hatIrgendeineBuchung: true));
    }
}
