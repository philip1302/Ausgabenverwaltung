namespace Ausgabenverwaltung.Core.YearInReview;

/// <summary>Welche der drei Kennzahlen ueber dem Rueckblick gemeint ist.</summary>
public enum ReviewMetricKind
{
    Ausgaben,
    Einnahmen,

    /// <summary>Einnahmen minus Ausgaben - was unterm Strich blieb.</summary>
    Netto,
}

/// <summary>
/// Eine der drei Kennzahlen, mit ihrem Vorjahreswert und der Veraenderung.
///
/// <see cref="IstVerbesserung"/> gibt es, weil dieselbe Rechnung je nach
/// Kennzahl das Gegenteil bedeutet: bei den Ausgaben ist weniger gut, bei
/// Einnahmen und Netto mehr. Die Entscheidung faellt hier und nicht in der
/// Ansicht - sie ist pruefbar (Regel 7).
/// </summary>
public sealed record ReviewMetric(
    ReviewMetricKind Kind,
    long PreviousCents,
    long CurrentCents,
    long DeltaCents,
    decimal? RelativeChange,
    bool IstVerbesserung);

/// <summary>
/// Die drei Zahlen ueber dem Rueckblick. Gebaut wird das an einer Stelle,
/// damit Netto nie von Ausgaben und Einnahmen abweichen kann.
/// </summary>
public sealed record ReviewHeadline(
    ReviewMetric Ausgaben,
    ReviewMetric Einnahmen,
    ReviewMetric Netto)
{
    /// <param name="vorjahrAusgabenCents">Ausgaben des Vorjahres, positiv.</param>
    /// <param name="jahrAusgabenCents">Ausgaben des Jahres, positiv.</param>
    public static ReviewHeadline Build(
        long vorjahrAusgabenCents,
        long jahrAusgabenCents,
        long vorjahrEinnahmenCents,
        long jahrEinnahmenCents)
    {
        var vorjahrNetto = vorjahrEinnahmenCents - vorjahrAusgabenCents;
        var jahrNetto = jahrEinnahmenCents - jahrAusgabenCents;

        return new ReviewHeadline(
            // Bei den Ausgaben ist weniger die gute Nachricht.
            Baue(ReviewMetricKind.Ausgaben, vorjahrAusgabenCents, jahrAusgabenCents,
                mehrIstBesser: false),
            Baue(ReviewMetricKind.Einnahmen, vorjahrEinnahmenCents, jahrEinnahmenCents,
                mehrIstBesser: true),
            Baue(ReviewMetricKind.Netto, vorjahrNetto, jahrNetto,
                mehrIstBesser: true));
    }

    private static ReviewMetric Baue(
        ReviewMetricKind kind, long vorjahr, long jahr, bool mehrIstBesser)
    {
        var delta = jahr - vorjahr;

        return new ReviewMetric(
            Kind: kind,
            PreviousCents: vorjahr,
            CurrentCents: jahr,
            DeltaCents: delta,
            RelativeChange: ReviewMath.RelativeChange(vorjahr, jahr),
            // Unveraendert ist keine Verbesserung - und auch keine
            // Verschlechterung. Die Ansicht hebt dann nichts hervor.
            IstVerbesserung: mehrIstBesser ? delta > 0 : delta < 0);
    }
}
