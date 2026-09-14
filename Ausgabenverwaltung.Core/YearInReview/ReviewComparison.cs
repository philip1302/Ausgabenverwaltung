namespace Ausgabenverwaltung.Core.YearInReview;

/// <summary>
/// Die fertige Gegenueberstellung zweier Jahre: der Kategoriebaum mit
/// beiden Jahreswerten und die Summen darueber. Ausgaben stehen positiv
/// (siehe <see cref="ReviewCategoryChange"/>).
///
/// Die Anzahlen stehen neben den Summen, weil sie etwas anderes
/// unterscheiden: eine Summe von 0 kann heissen "Ausgabe und Erstattung
/// heben sich auf" oder "in diesem Jahr wurde nichts gebucht". Nur der
/// zweite Fall darf zu "Fuer 2025 liegt keine Buchung vor" fuehren.
/// </summary>
public sealed record ReviewComparison(
    ReviewComparisonPeriods Periods,
    IReadOnlyList<ReviewCategoryChange> Roots,
    long PreviousTotalCents,
    long CurrentTotalCents,
    int PreviousTotalCount,
    int CurrentTotalCount)
{
    /// <summary>Groesser null = insgesamt mehr ausgegeben als im Vorjahr.</summary>
    public long DeltaCents => CurrentTotalCents - PreviousTotalCents;

    public bool VorjahrHatBuchungen => PreviousTotalCount > 0;
    public bool JahrHatBuchungen => CurrentTotalCount > 0;
}
