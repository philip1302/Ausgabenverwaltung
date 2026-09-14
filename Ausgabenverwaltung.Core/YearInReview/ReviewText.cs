using System.Globalization;
using Ausgabenverwaltung.Core.Formatting;

namespace Ausgabenverwaltung.Core.YearInReview;

/// <summary>
/// Die Saetze des Jahresrueckblicks. Sie stehen in Core und nicht in der
/// Ansicht, weil sie pruefbar sein muessen (Regel 7) - ein Rueckblick, der
/// "-1.240,00 €" sagt statt "1.240,00 € mehr", hat seine Aufgabe verfehlt.
///
/// <b>Die Richtung steckt immer im WORT, nie im Vorzeichen.</b> Jeder
/// Betrag geht ueber <c>EuroText.Format(Math.Abs(...))</c>; ob es mehr oder
/// weniger war, sagt der Satz. Ein Test prueft das ueber alle Befundarten
/// hinweg mit.
/// </summary>
public static class ReviewText
{
    private static readonly CultureInfo DeDe = CultureInfo.GetCultureInfo("de-DE");

    // Dieselbe Ueberlegung wie in EuroText: zwischen Zahl und Zeichen ein
    // geschuetztes Leerzeichen, damit der Umbruch die beiden nicht trennt.
    private const char NonBreakingSpace = (char)0x00A0;

    /// <summary>Die Ueberschrift einer Karte.</summary>
    public static string Ueberschrift(ReviewFindingKind art) => art switch
    {
        ReviewFindingKind.StaerksteSteigerung => "Am stärksten gestiegen",
        ReviewFindingKind.StaerksterRueckgang => "Am stärksten gesunken",
        ReviewFindingKind.NeuHinzugekommen => "Neu dabei",
        ReviewFindingKind.Weggefallen => "Ganz weggefallen",
        ReviewFindingKind.GroessterKostenblock => "Größter Posten",
        ReviewFindingKind.VieleKleineBuchungen => "Viele kleine Beträge",
        ReviewFindingKind.HaeufigsteKategorie => "Am häufigsten gebucht",
        ReviewFindingKind.TeuersterMonat => "Teuerster Monat",
        ReviewFindingKind.GuenstigsterMonat => "Ruhigster Monat",
        _ => throw new ArgumentOutOfRangeException(nameof(art)),
    };

    /// <summary>Der ganze Satz zu einem Befund.</summary>
    public static string Satz(ReviewFinding befund)
    {
        var jetzt = Betrag(befund.CurrentCents);
        var vorher = Betrag(befund.PreviousCents);
        var unterschied = Betrag(befund.DeltaCents);

        return befund.Kind switch
        {
            ReviewFindingKind.StaerksteSteigerung =>
                $"Für {befund.Subject} sind {jetzt} zusammengekommen, {unterschied} mehr "
                + $"als im Vorjahr ({ProzentText(befund.RelativeChange)}).",

            ReviewFindingKind.StaerksterRueckgang =>
                $"Für {befund.Subject} sind {jetzt} zusammengekommen, {unterschied} weniger "
                + $"als im Vorjahr ({ProzentText(befund.RelativeChange)}).",

            ReviewFindingKind.NeuHinzugekommen =>
                $"{befund.Subject} gab es im Vorjahr noch nicht. Seither sind {jetzt} "
                + "zusammengekommen.",

            ReviewFindingKind.Weggefallen =>
                $"Für {befund.Subject} wurde nichts mehr gebucht. Im Vorjahr waren es "
                + $"noch {vorher}.",

            ReviewFindingKind.GroessterKostenblock =>
                $"{befund.Subject} ist dein größter Posten: {jetzt}, das sind "
                + $"{AnteilText(befund.Share)} aller Ausgaben.",

            ReviewFindingKind.VieleKleineBuchungen =>
                $"In {befund.Subject} sind {Anzahl(befund.CurrentCount)} zusammengekommen, "
                + $"im Schnitt {Betrag(Schnitt(befund))} — zusammen {jetzt}.",

            ReviewFindingKind.HaeufigsteKategorie =>
                $"Am häufigsten gebucht hast du {befund.Subject}: "
                + $"{Anzahl(befund.CurrentCount)} mit zusammen {jetzt}, "
                + $"{AnzahlVergleich(befund)}.",

            ReviewFindingKind.TeuersterMonat =>
                $"Im {befund.Subject} hast du am meisten ausgegeben: {jetzt}, "
                + $"{unterschied} über deinem Monatsschnitt.",

            ReviewFindingKind.GuenstigsterMonat =>
                $"Der {befund.Subject} war dein ruhigster Monat: {jetzt}, "
                + $"{unterschied} unter deinem Monatsschnitt.",

            _ => throw new ArgumentOutOfRangeException(nameof(befund)),
        };
    }

    /// <summary>
    /// Alles, was hinter einer Karte steht - der Inhalt des Fensters am
    /// Fragezeichen. Die Zeitraeume werden mitgegeben, weil die Zahlen
    /// ihre Jahresbeschriftung tragen ("2026" / "2025") und eine Karte
    /// von sich aus nicht weiss, welche Jahre verglichen werden.
    /// </summary>
    public static ReviewExplanation Erklaerung(
        ReviewFinding befund, ReviewComparisonPeriods zeitraeume) =>
        new(Ueberschrift(befund.Kind),
            befund.Subject,
            Satz(befund),
            Bedeutung(befund.Kind),
            Werte(befund, zeitraeume));

    /// <summary>
    /// Wonach gesucht wurde, dass ausgerechnet diese Karte hier steht.
    /// Bewusst ohne Zahlen: die stehen daneben, und wer die Auswahlregel
    /// wissen will, will sie unabhaengig vom Einzelfall lesen.
    /// </summary>
    public static string Bedeutung(ReviewFindingKind art) => art switch
    {
        ReviewFindingKind.StaerksteSteigerung =>
            "Von allen Kategorien ist das die, für die dieses Jahr am meisten mehr "
            + "zusammengekommen ist als im Vorjahr. Verglichen wird der Betrag und nicht "
            + "der Prozentsatz — sonst stünde hier jedes Mal ein kleiner Posten, der sich "
            + "zufällig verdoppelt hat.",

        ReviewFindingKind.StaerksterRueckgang =>
            "Von allen Kategorien ist das die, für die dieses Jahr am meisten weniger "
            + "zusammengekommen ist als im Vorjahr. Gemessen wird auch hier der Betrag, "
            + "damit kleine Beträge mit großem Ausschlag die Liste nicht anführen.",

        ReviewFindingKind.NeuHinzugekommen =>
            "Hier gab es im Vorjahreszeitraum keine einzige Buchung und dieses Jahr "
            + "welche. Die Kategorie ist also neu dazugekommen und nicht bloß teurer "
            + "geworden.",

        ReviewFindingKind.Weggefallen =>
            "Im Vorjahreszeitraum wurde hier gebucht, dieses Jahr kein einziges Mal mehr. "
            + "Ob das so gewollt war oder ob etwas zu erfassen vergessen wurde, sagt der "
            + "Rückblick nicht — er zeigt es nur.",

        ReviewFindingKind.GroessterKostenblock =>
            "Die oberste Kategorie mit dem größten Anteil an den Ausgaben des Jahres. "
            + "Gezählt wird der ganze Ast, also auch alles, was in ihren Unterkategorien "
            + "gebucht ist.",

        ReviewFindingKind.VieleKleineBuchungen =>
            "Hier kommen viele kleine Beträge zusammen, jeder für sich unauffällig. Eine "
            + "Liste nach Summen zeigt so etwas nie — deshalb hat es eine eigene Karte.",

        ReviewFindingKind.HaeufigsteKategorie =>
            "Die Kategorie mit den meisten einzelnen Buchungen, unabhängig davon, wie "
            + "viel Geld darin steckt. Mitgezählt werden nur Kategorien, in die "
            + "unmittelbar gebucht wird.",

        ReviewFindingKind.TeuersterMonat =>
            "Der Monat mit den höchsten Ausgaben des Jahres. Verglichen wird er mit "
            + "deinem eigenen Monatsschnitt in diesem Jahr, nicht mit demselben Monat "
            + "im Vorjahr.",

        ReviewFindingKind.GuenstigsterMonat =>
            "Der Monat, der am deutlichsten unter deinem Monatsschnitt liegt. Er ist nie "
            + "derselbe wie der teuerste Monat.",

        _ => throw new ArgumentOutOfRangeException(nameof(art)),
    };

    // Die Zahlen unter dem Satz. Zwei Faelle, die sich nicht vermischen
    // lassen: ein Kategoriebefund vergleicht zwei JAHRE, ein Monatsbefund
    // den Monat mit dem MONATSSCHNITT (siehe ReviewFindings.BaueMonat).
    // Stuende ueber dem Schnitt eine Jahreszahl, waere die Zahl darunter
    // schlicht falsch beschriftet.
    private static IReadOnlyList<ReviewValue> Werte(
        ReviewFinding befund, ReviewComparisonPeriods zeitraeume)
    {
        var werte = new List<ReviewValue>();

        if (befund.CategoryId is null)
        {
            werte.Add(new ReviewValue("In diesem Monat", Betrag(befund.CurrentCents)));
            werte.Add(new ReviewValue("Dein Monatsschnitt", Betrag(befund.PreviousCents)));
            werte.Add(new ReviewValue("Unterschied zum Schnitt", Veraenderung(befund.DeltaCents)));
        }
        else
        {
            werte.Add(new ReviewValue(
                zeitraeume.CurrentLabel, BetragOderNichts(befund.CurrentCents, befund.CurrentCount)));
            werte.Add(new ReviewValue(
                zeitraeume.PreviousLabel, BetragOderNichts(befund.PreviousCents, befund.PreviousCount)));
            werte.Add(new ReviewValue("Unterschied", Veraenderung(befund.DeltaCents)));

            // Die Prozentzahl nur, wo sie etwas hinzufuegt: bei "neu" und
            // "nicht mehr gebucht" stuende sie in Worten schon in der Zeile
            // darueber, und "unverändert" zweimal untereinander ist keine
            // Auskunft, sondern eine Wiederholung.
            if (befund.RelativeChange is decimal wert && wert is > -1m and not 0m)
            {
                werte.Add(new ReviewValue("Prozentual", ProzentText(befund.RelativeChange)));
            }
        }

        if (befund.Share > 0m)
        {
            werte.Add(new ReviewValue("Anteil an den Ausgaben", AnteilText(befund.Share)));
        }

        // Ohne Buchung gibt es nichts zu zaehlen - genau der Fall "ganz
        // weggefallen".
        if (befund.CurrentCount > 0)
        {
            werte.Add(new ReviewValue("Buchungen", Buchungen(befund)));
        }

        // Der Schnitt erst ab der zweiten Buchung: bei genau einer stuende
        // dort noch einmal derselbe Betrag wie oben.
        if (befund.CurrentCount > 1)
        {
            werte.Add(new ReviewValue("Im Schnitt je Buchung", Betrag(Schnitt(befund))));
        }

        return werte;
    }

    private static string Buchungen(ReviewFinding befund) =>
        befund.CategoryId is null
            ? Anzahl(befund.CurrentCount)
            : $"{Anzahl(befund.CurrentCount)}, {AnzahlVergleich(befund)}";

    // Ein Jahr ohne eine einzige Buchung bekommt Worte und keine Null -
    // dieselbe Unterscheidung wie in der Tabelle darunter: 0,00 € kann
    // auch heissen, dass sich Ausgabe und Erstattung aufgehoben haben.
    private static string BetragOderNichts(long cents, int anzahl) =>
        anzahl > 0 ? Betrag(cents) : "keine Buchung";

    /// <summary>
    /// Der Unterschied in Worten: "412,00 € mehr", "88,00 € weniger",
    /// "unverändert". Nie mit einem Minuszeichen davor.
    /// </summary>
    public static string Veraenderung(long deltaCents) => deltaCents switch
    {
        0 => "unverändert",
        > 0 => Betrag(deltaCents) + " mehr",
        _ => Betrag(deltaCents) + " weniger",
    };

    /// <summary>
    /// Die Veraenderung als Prozentangabe. Ohne Vorjahreswert gibt es
    /// keine Zahl, sondern das Wort dafuer - siehe
    /// <see cref="ReviewMath.RelativeChange"/>.
    /// </summary>
    public static string ProzentText(decimal? relative)
    {
        if (relative is not decimal wert)
        {
            return "neu";
        }

        if (wert <= -1m)
        {
            return "nicht mehr gebucht";
        }

        var prozent = Math.Round(Math.Abs(wert) * 100m, MidpointRounding.AwayFromZero);
        if (prozent == 0m)
        {
            return "unverändert";
        }

        var zahl = prozent.ToString("0", DeDe) + NonBreakingSpace + "%";
        return wert > 0 ? zahl + " mehr" : zahl + " weniger";
    }

    /// <summary>Ein Anteil als Prozentangabe ohne Richtung, z. B. "18 %".</summary>
    public static string AnteilText(decimal anteil) =>
        Math.Round(anteil * 100m, MidpointRounding.AwayFromZero).ToString("0", DeDe)
        + NonBreakingSpace + "%";

    /// <summary>
    /// Der Satz ueber den Zahlen, der sagt, worauf sich der Vergleich
    /// stuetzt. Er steht auch dann da, wenn ganze Kalenderjahre gewaehlt
    /// sind und das laufende noch nicht zu Ende ist - sonst sieht der
    /// fehlende Rest des Jahres wie ein Sparerfolg aus.
    /// </summary>
    public static string ZeitraumHinweis(ReviewComparisonPeriods zeitraeume)
    {
        var jahre = $"{zeitraeume.CurrentLabel} und {zeitraeume.PreviousLabel}";

        return zeitraeume.IsPartial
            ? $"Verglichen wird für {jahre} jeweils {zeitraeume.SpanCaption}. "
              + $"{zeitraeume.CurrentLabel} läuft noch."
            : $"Verglichen werden die ganzen Kalenderjahre {jahre}.";
    }

    /// <summary>
    /// Zusatz zum Hinweis, wenn ganze Kalenderjahre gewaehlt sind, obwohl
    /// das Jahr noch laeuft. Leer, wenn es nichts zu warnen gibt.
    /// </summary>
    public static string GanzjahresWarnung(ReviewComparisonPeriods zeitraeume, DateOnly heute) =>
        !zeitraeume.IsPartial && ReviewPeriods.IstLaufendesJahr(zeitraeume.Year, heute)
            ? $"{zeitraeume.CurrentLabel} ist noch nicht zu Ende – die Zahlen des Jahres "
              + "sind deshalb niedriger, als sie am Jahresende sein werden."
            : string.Empty;

    /// <summary>Die Beschriftung einer der drei Kennzahlen.</summary>
    public static string KennzahlName(ReviewMetricKind art) => art switch
    {
        ReviewMetricKind.Ausgaben => "Ausgaben",
        ReviewMetricKind.Einnahmen => "Einnahmen",
        ReviewMetricKind.Netto => "Unterm Strich",
        _ => throw new ArgumentOutOfRangeException(nameof(art)),
    };

    /// <summary>
    /// Die Veraenderung einer Kennzahl in Worten, z. B.
    /// "1.880,00 € mehr als 2025".
    /// </summary>
    public static string KennzahlVeraenderung(ReviewMetric kennzahl, string vorjahrLabel) =>
        kennzahl.DeltaCents == 0
            ? $"genauso viel wie {vorjahrLabel}"
            : $"{Veraenderung(kennzahl.DeltaCents)} als {vorjahrLabel}";

    /// <summary>
    /// Der Satz zur Lage - was an die Stelle der Karten tritt, wenn es
    /// nichts zu berichten gibt. Bei
    /// <see cref="ReviewDataState.Vollstaendig"/> leer.
    /// </summary>
    public static string LageHinweis(
        ReviewDataState lage, ReviewComparisonPeriods zeitraeume) => lage switch
    {
        ReviewDataState.NochNichtsErfasst =>
            "Noch ist keine Buchung erfasst. Ein Rückblick braucht zwei Jahre mit Zahlen.",

        ReviewDataState.VorjahrOhneBuchung =>
            $"Für {zeitraeume.PreviousLabel} liegt keine Buchung vor. Ein Vergleich "
            + "braucht zwei Jahre – wähle oben ein Jahr, für das es beide gibt.",

        // Ausdruecklich ein Ergebnis und kein Mangel: es gibt nichts
        // aufzuloesen, deshalb steht daneben auch kein Knopf.
        ReviewDataState.KeineAuffaelligkeiten =>
            $"Zwischen {zeitraeume.PreviousLabel} und {zeitraeume.CurrentLabel} hat sich "
            + "nichts deutlich verschoben.",

        _ => string.Empty,
    };

    // Betraege gehen IMMER ueber den Betrag ihres Werts. Die Richtung
    // steht im Satz - das ist der ganze Trick, und er steht genau hier.
    private static string Betrag(long cents) => EuroText.Format(Math.Abs(cents));

    private static string Anzahl(int anzahl) =>
        anzahl == 1 ? "eine Buchung" : $"{anzahl.ToString("N0", DeDe)} Buchungen";

    private static long Schnitt(ReviewFinding befund) =>
        befund.CurrentCount == 0 ? 0 : befund.CurrentCents / befund.CurrentCount;

    private static string AnzahlVergleich(ReviewFinding befund)
    {
        var unterschied = befund.CurrentCount - befund.PreviousCount;

        return unterschied switch
        {
            0 => "genauso oft wie im Vorjahr",
            > 0 => $"{unterschied} mehr als im Vorjahr",
            _ => $"{-unterschied} weniger als im Vorjahr",
        };
    }
}
