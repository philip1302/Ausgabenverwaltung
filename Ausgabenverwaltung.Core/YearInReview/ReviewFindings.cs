namespace Ausgabenverwaltung.Core.YearInReview;

/// <summary>
/// Der Kern des Jahresrueckblicks: aus der Gegenueberstellung zweier Jahre
/// werden die Punkte herausgesucht, die eine Meldung wert sind, und in
/// eine Rangfolge gebracht.
///
/// Eine reine Funktion - keine Datenbank, kein heutiges Datum, kein
/// Zufall. Dieselben Eingaben ergeben immer dieselbe Liste, auch in der
/// Reihenfolge; darauf beruhen die Tests.
///
/// Drei Dinge passieren hier, und jedes loest ein Problem, das ohne es die
/// ganze Liste unbrauchbar machen wuerde:
///
/// 1. <b>Entrauschen.</b> Eine Ausgabe von 2 auf 20 Euro ist plus 900
///    Prozent. Ohne Schwellen (<see cref="ReviewThresholds"/>) fuehrt
///    solches Kleinzeug jede Rangliste an.
/// 2. <b>Verdichten.</b> Die Auswertung summiert astweise auf, derselbe
///    Zuwachs steht also zugleich in "Wohnen", "Wohnen &gt; Nebenkosten"
///    und "Wohnen &gt; Nebenkosten &gt; Strom". Gemeldet wird nur die
///    genaueste Stelle, die ihn erklaert (siehe <see cref="IstMeldeknoten"/>).
/// 3. <b>Sperren.</b> Jede Kategorie kommt hoechstens einmal vor und jede
///    Befundart hoechstens einmal - "die staerkste Steigerung" gibt es nur
///    in der Einzahl. Vorfahre und Nachfahre duerfen dagegen
///    nebeneinanderstehen: "groesster Kostenblock: Wohnen" und "haeufigste
///    Kategorie: Wohnen &gt; Kiosk" sind zwei Aussagen und keine Dopplung.
///    Dass dieselbe VERSCHIEBUNG nicht auf mehreren Stufen gemeldet wird,
///    erledigt bereits die Meldeknoten-Regel.
///
/// Ausgewaehlt wird in zwei Durchgaengen: erst die Veraenderungen, dann die
/// Dauerzustaende. Eine Verschiebung ist immer die bessere Nachricht als
/// eine Tatsache, die letztes Jahr auch schon galt - ohne den Vorrang
/// koennte "das ist dein groesster Kostenblock" die Steigerung derselben
/// Kategorie verdraengen, nur weil die Summe groesser ist als der
/// Unterschied.
/// </summary>
public static class ReviewFindings
{
    /// <summary>
    /// Die Befunde zu einer Gegenueberstellung, nach Rangzahl absteigend.
    /// </summary>
    /// <param name="currentMonths">
    /// Die Monate des betrachteten Zeitraums, LUECKENLOS - ein Monat ohne
    /// Buchung muss mit 0 dabei sein, sonst stimmt der Durchschnitt nicht.
    /// Das Vorjahr wird hier nicht gebraucht: ein auffaelliger Monat misst
    /// sich am eigenen Jahr, nicht am Vorjahresmonat.
    /// </param>
    public static IReadOnlyList<ReviewFinding> Build(
        ReviewComparison comparison,
        IReadOnlyList<ReviewMonth> currentMonths,
        int maxFindings = ReviewThresholds.HoechsteBefundzahl)
    {
        if (maxFindings <= 0)
        {
            return Array.Empty<ReviewFinding>();
        }

        // Zu duenne Datenlage fuer eine Aussage ueber Gewohnheiten. Lieber
        // gar nichts sagen als aus drei Buchungen ein Jahr deuten.
        if (comparison.CurrentTotalCents < ReviewThresholds.MindestJahresausgabenCents &&
            comparison.PreviousTotalCents < ReviewThresholds.MindestJahresausgabenCents)
        {
            return Array.Empty<ReviewFinding>();
        }

        var knoten = new List<ReviewCategoryChange>();
        Sammle(comparison.Roots, knoten);

        var kandidaten = new List<ReviewFinding>();
        kandidaten.AddRange(Veraenderungen(knoten, comparison));
        kandidaten.AddRange(Kostenblock(knoten, comparison));
        kandidaten.AddRange(Kleinvieh(knoten, comparison));
        kandidaten.AddRange(Haeufigste(knoten, comparison));
        kandidaten.AddRange(Monatsbefunde(currentMonths, comparison));

        return Rangiere(kandidaten, maxFindings);
    }

    /// <summary>
    /// In welcher Lage sich die Seite befindet - siehe
    /// <see cref="ReviewDataState"/>.
    /// </summary>
    /// <param name="hatIrgendeineBuchung">
    /// Ob die Datenbank ueberhaupt eine Buchung enthaelt, unabhaengig vom
    /// gewaehlten Jahr (ExpenseRepository.HasAny).
    /// </param>
    public static ReviewDataState Bewerte(
        ReviewComparison comparison,
        IReadOnlyList<ReviewFinding> findings,
        bool hatIrgendeineBuchung)
    {
        if (!hatIrgendeineBuchung)
        {
            return ReviewDataState.NochNichtsErfasst;
        }

        if (!comparison.VorjahrHatBuchungen)
        {
            return ReviewDataState.VorjahrOhneBuchung;
        }

        return findings.Count == 0
            ? ReviewDataState.KeineAuffaelligkeiten
            : ReviewDataState.Vollstaendig;
    }

    // ----------------------------------------------------------------
    // Kandidaten
    // ----------------------------------------------------------------

    private static IEnumerable<ReviewFinding> Veraenderungen(
        IReadOnlyList<ReviewCategoryChange> knoten, ReviewComparison comparison)
    {
        foreach (var k in knoten)
        {
            var delta = k.DeltaCents;
            var betrag = Math.Abs(delta);

            if (betrag < ReviewThresholds.MindestDeltaCents)
            {
                continue;
            }

            if (Math.Max(k.PreviousCents, k.CurrentCents) <
                ReviewThresholds.MindestVolumenCents)
            {
                continue;
            }

            // Die relative Schwelle greift nur, wo es einen Vorjahreswert
            // gibt, an dem sich etwas messen liesse.
            if (k.PreviousCents > 0 &&
                betrag < ReviewThresholds.MindestRelativ * k.PreviousCents)
            {
                continue;
            }

            if (!IstMeldeknoten(k))
            {
                continue;
            }

            var art = Art(k);
            if (art is null)
            {
                continue;
            }

            yield return Baue(art.Value, k, comparison, ReviewMath.Score(
                k.PreviousCents, k.CurrentCents));
        }
    }

    // "Neu" und "weggefallen" richten sich nach der ANZAHL und nicht nach
    // der Summe: eine Kategorie, in der sich Ausgabe und Erstattung
    // aufheben, hat Summe null, war aber sehr wohl in Gebrauch. Ohne
    // Vorjahreswert gibt es ausserdem keine Prozentzahl - eine Steigerung
    // ohne Grundlage waere nicht auszudruecken, deshalb faellt sie hier
    // heraus statt spaeter einen Satz ohne Aussage zu erzeugen.
    private static ReviewFindingKind? Art(ReviewCategoryChange k)
    {
        if (k.PreviousCount == 0)
        {
            return ReviewFindingKind.NeuHinzugekommen;
        }

        if (k.CurrentCount == 0)
        {
            return ReviewFindingKind.Weggefallen;
        }

        if (k.PreviousCents == 0 || k.CurrentCents == 0)
        {
            return null;
        }

        return k.DeltaCents > 0
            ? ReviewFindingKind.StaerksteSteigerung
            : ReviewFindingKind.StaerksterRueckgang;
    }

    /// <summary>
    /// Ob dieser Knoten die genaueste Stelle ist, an der die Veraenderung
    /// noch eine Aussage ist.
    ///
    /// Traegt ein direktes Kind denselben Vorzeichenwechsel und mindestens
    /// <see cref="ReviewThresholds.AnteilFuerAbstieg"/> des Unterschieds,
    /// dann erklaert das Kind die Veraenderung und wird an seiner Stelle
    /// gemeldet - der Knoten selbst faellt heraus. Verteilt sie sich
    /// dagegen auf mehrere Zweige, ist sie nur hier oben eine Aussage.
    ///
    /// Ein gegenlaeufiges Kind verdraengt die Oberkategorie nie: dass
    /// woanders gespart wurde, erklaert keinen Zuwachs.
    /// </summary>
    private static bool IstMeldeknoten(ReviewCategoryChange k)
    {
        var delta = k.DeltaCents;
        if (delta == 0)
        {
            return true;
        }

        var schwelle = ReviewThresholds.AnteilFuerAbstieg * Math.Abs(delta);

        foreach (var kind in k.Children)
        {
            if (Math.Sign(kind.DeltaCents) != Math.Sign(delta))
            {
                continue;
            }

            if (Math.Abs(kind.DeltaCents) >= schwelle)
            {
                return false;
            }
        }

        return true;
    }

    // Der groesste Kostenblock ist immer eine OBERkategorie: die Frage
    // dahinter lautet "wofuer geht das meiste weg", und die Antwort
    // "Lebensmittel" ist brauchbarer als "Lebensmittel > Supermarkt > Rewe".
    private static IEnumerable<ReviewFinding> Kostenblock(
        IReadOnlyList<ReviewCategoryChange> knoten, ReviewComparison comparison)
    {
        var groesster = knoten
            .Where(k => k.Depth == 0 && k.CurrentCents >= ReviewThresholds.MindestVolumenCents)
            .OrderByDescending(k => k.CurrentCents)
            .ThenBy(k => k.CategoryId)
            .FirstOrDefault();

        if (groesster is null)
        {
            yield break;
        }

        yield return Baue(
            ReviewFindingKind.GroessterKostenblock, groesster, comparison,
            groesster.CurrentCents / ReviewThresholds.TeilerKostenblock);
    }

    // Beide anzahlgetriebenen Befunde sehen nur BLAETTER an. In eine
    // Oberkategorie wird selten unmittelbar gebucht; ihre Anzahl ist die
    // Summe ihres Astes und beantwortet die Frage "wo buchst du staendig"
    // nicht.
    private static IEnumerable<ReviewFinding> Kleinvieh(
        IReadOnlyList<ReviewCategoryChange> knoten, ReviewComparison comparison)
    {
        var treffer = knoten
            .Where(k => k.IstBlatt
                        && k.CurrentCount >= ReviewThresholds.KleinviehMindestAnzahl
                        && k.CurrentCents >= ReviewThresholds.KleinviehMindestSummeCents
                        && k.CurrentCents / k.CurrentCount
                           <= ReviewThresholds.KleinviehHoechstSchnittCents)
            .OrderByDescending(k => k.CurrentCents)
            .ThenBy(k => k.CategoryId)
            .FirstOrDefault();

        if (treffer is null)
        {
            yield break;
        }

        yield return Baue(
            ReviewFindingKind.VieleKleineBuchungen, treffer, comparison,
            treffer.CurrentCents / ReviewThresholds.TeilerKleinvieh);
    }

    private static IEnumerable<ReviewFinding> Haeufigste(
        IReadOnlyList<ReviewCategoryChange> knoten, ReviewComparison comparison)
    {
        var treffer = knoten
            .Where(k => k.IstBlatt
                        && k.CurrentCount >= ReviewThresholds.HaeufigsteMindestAnzahl)
            .OrderByDescending(k => k.CurrentCount)
            .ThenByDescending(k => k.CurrentCents)
            .ThenBy(k => k.CategoryId)
            .FirstOrDefault();

        if (treffer is null)
        {
            yield break;
        }

        yield return Baue(
            ReviewFindingKind.HaeufigsteKategorie, treffer, comparison,
            treffer.CurrentCents / ReviewThresholds.TeilerHaeufigste);
    }

    // Gemessen wird der UEBERSCHUSS beziehungsweise der Fehlbetrag
    // gegenueber dem eigenen Monatsschnitt - dass ein Monat ueberhaupt
    // Ausgaben hat, ist keine Nachricht. Teuerster und guenstigster Monat
    // koennen dadurch nie derselbe sein.
    private static IEnumerable<ReviewFinding> Monatsbefunde(
        IReadOnlyList<ReviewMonth> monate, ReviewComparison comparison)
    {
        if (monate.Count < ReviewThresholds.MindestMonateFuerMonatsbefund)
        {
            yield break;
        }

        var summe = monate.Sum(m => m.ExpenseCents);
        var durchschnitt = Money.ToCents(Money.ToDecimal(summe) / monate.Count);

        var teuerster = monate
            .OrderByDescending(m => m.ExpenseCents)
            .ThenBy(m => m.Key, StringComparer.Ordinal)
            .First();

        if (teuerster.ExpenseCents - durchschnitt >= ReviewThresholds.MindestDeltaCents)
        {
            yield return BaueMonat(
                ReviewFindingKind.TeuersterMonat, teuerster, durchschnitt, comparison);
        }

        var guenstigster = monate
            .OrderBy(m => m.ExpenseCents)
            .ThenBy(m => m.Key, StringComparer.Ordinal)
            .First();

        if (guenstigster.Key != teuerster.Key &&
            durchschnitt - guenstigster.ExpenseCents >= ReviewThresholds.MindestDeltaCents)
        {
            yield return BaueMonat(
                ReviewFindingKind.GuenstigsterMonat, guenstigster, durchschnitt, comparison);
        }
    }

    // ----------------------------------------------------------------
    // Rangfolge
    // ----------------------------------------------------------------

    /// <summary>
    /// Ob eine Befundart von einer Veraenderung zwischen den Jahren
    /// spricht - im Gegensatz zu einem Dauerzustand, der letztes Jahr
    /// genauso galt.
    /// </summary>
    private static bool IstVeraenderung(ReviewFindingKind art) => art is
        ReviewFindingKind.StaerksteSteigerung or
        ReviewFindingKind.StaerksterRueckgang or
        ReviewFindingKind.NeuHinzugekommen or
        ReviewFindingKind.Weggefallen;

    private static IReadOnlyList<ReviewFinding> Rangiere(
        IReadOnlyList<ReviewFinding> kandidaten, int maxFindings)
    {
        var gesperrt = new HashSet<int>();
        var arten = new HashSet<ReviewFindingKind>();
        var ergebnis = new List<ReviewFinding>();

        // Erst die Veraenderungen, dann die Dauerzustaende - und innerhalb
        // beider Gruppen nach Punktzahl. Bei Gleichstand entscheiden Art,
        // Kategorie und Zeitabschnitt, damit die Reihenfolge nicht von der
        // Laune der Sortierung abhaengt.
        Nimm(kandidaten.Where(b => IstVeraenderung(b.Kind)));
        Nimm(kandidaten.Where(b => !IstVeraenderung(b.Kind)));

        // Angezeigt wird trotzdem nach Punktzahl: der Vorrang steuert die
        // AUSWAHL, nicht die Leserichtung.
        return ergebnis
            .OrderByDescending(b => b.Score)
            .ThenBy(b => (int)b.Kind)
            .ToList();

        void Nimm(IEnumerable<ReviewFinding> gruppe)
        {
            var sortiert = gruppe
                .OrderByDescending(b => b.Score)
                .ThenBy(b => (int)b.Kind)
                .ThenBy(b => b.CategoryId ?? int.MaxValue)
                .ThenBy(b => b.PeriodKey ?? string.Empty, StringComparer.Ordinal);

            foreach (var befund in sortiert)
            {
                if (ergebnis.Count >= maxFindings)
                {
                    return;
                }

                if (arten.Contains(befund.Kind))
                {
                    continue;
                }

                // Dieselbe Kategorie nicht zweimal - zwei Karten ueber
                // denselben Posten sagen zusammen weniger als eine.
                if (befund.CategoryId is int id && !gesperrt.Add(id))
                {
                    continue;
                }

                ergebnis.Add(befund);
                arten.Add(befund.Kind);
            }
        }
    }

    // ----------------------------------------------------------------
    // Hilfsmittel
    // ----------------------------------------------------------------

    private static ReviewFinding Baue(
        ReviewFindingKind art, ReviewCategoryChange k, ReviewComparison comparison, long score) =>
        new()
        {
            Kind = art,
            CategoryId = k.CategoryId,
            Subject = k.FullPath,
            PeriodKey = null,
            PreviousCents = k.PreviousCents,
            CurrentCents = k.CurrentCents,
            DeltaCents = k.DeltaCents,
            RelativeChange = ReviewMath.RelativeChange(k.PreviousCents, k.CurrentCents),
            Share = ReviewMath.Share(k.CurrentCents, comparison.CurrentTotalCents),
            PreviousCount = k.PreviousCount,
            CurrentCount = k.CurrentCount,
            Score = score,
        };

    // Bei einem Monatsbefund ist der Vergleichswert der MONATSSCHNITT und
    // nicht das Vorjahr. Er steht trotzdem in PreviousCents, damit die
    // Anzeige einen einzigen Satzbau hat ("2.980 statt 1.740") - welcher
    // Vergleich gemeint ist, sagt die Befundart.
    private static ReviewFinding BaueMonat(
        ReviewFindingKind art,
        ReviewMonth monat,
        long durchschnittCents,
        ReviewComparison comparison) =>
        new()
        {
            Kind = art,
            CategoryId = null,
            Subject = monat.Label,
            PeriodKey = monat.Key,
            PreviousCents = durchschnittCents,
            CurrentCents = monat.ExpenseCents,
            DeltaCents = monat.ExpenseCents - durchschnittCents,
            RelativeChange = ReviewMath.RelativeChange(durchschnittCents, monat.ExpenseCents),
            Share = ReviewMath.Share(monat.ExpenseCents, comparison.CurrentTotalCents),
            PreviousCount = 0,
            CurrentCount = monat.Count,
            Score = Math.Abs(monat.ExpenseCents - durchschnittCents)
                    * ReviewThresholds.FaktorMonatsabweichung,
        };

    // Der Baum flach, damit jede Kategorie einmal als Kandidat geprueft
    // wird - auf welcher Stufe sie haengt, steht in ihrer Tiefe.
    private static void Sammle(
        IReadOnlyList<ReviewCategoryChange> knoten, List<ReviewCategoryChange> ziel)
    {
        foreach (var k in knoten)
        {
            ziel.Add(k);
            Sammle(k.Children, ziel);
        }
    }
}
