using System.Globalization;
using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.Core.YearInReview;

/// <summary>
/// Die Datumsarithmetik des Jahresrueckblicks: aus einer Jahreszahl, der
/// gewuenschten Spannweite und dem heutigen Tag werden die beiden
/// Zeitraeume, die verglichen werden.
///
/// Liegt in Core und bekommt das heutige Datum uebergeben statt es selbst
/// zu lesen - genauso wie <see cref="DateRangePresets"/> und aus demselben
/// Grund: Jahreswechsel und Schaltjahre muessen pruefbar sein (Regel 7).
/// </summary>
public static class ReviewPeriods
{
    // Fest verdrahtet statt ueber CultureInfo - dieselbe Ueberlegung wie
    // bei den Monatskuerzeln in ReportPeriods: die Beschriftung soll auf
    // jedem Rechner gleich aussehen, unabhaengig davon, welche Namen die
    // installierten Gebietsschema-Daten gerade liefern.
    private static readonly string[] MonthNames =
    {
        "Januar", "Februar", "März", "April", "Mai", "Juni",
        "Juli", "August", "September", "Oktober", "November", "Dezember",
    };

    /// <summary>
    /// Ob das Jahr noch laeuft. Nur dann hat die Wahl der Spannweite
    /// ueberhaupt eine Wirkung - ein abgeschlossenes Jahr wird immer ganz
    /// verglichen, und die Oberflaeche blendet den Umschalter aus.
    /// </summary>
    public static bool IstLaufendesJahr(int year, DateOnly today) => year == today.Year;

    /// <summary>
    /// Die beiden Zeitraeume fuer den Rueckblick auf <paramref name="year"/>.
    ///
    /// Bei <see cref="ReviewSpan.GleicherZeitraum"/> und laufendem Jahr
    /// enden beide Zeitraeume am selben Tag-und-Monat. Ausgerichtet wird
    /// dabei am EINschliessenden Enddatum und nicht an einer Tageszahl:
    /// <see cref="DateOnly.AddYears"/> kuerzt den 29. Februar von sich aus
    /// auf den 28. - dieselbe Regel, die schon fuer die wiederkehrenden
    /// Buchungen gilt (Regel 5). Ueber eine reine Tageszaehlung ragte der
    /// Vorjahreszeitraum stattdessen in den Maerz hinein und liesse sich
    /// nicht mehr benennen ("1. Januar bis 1. Maerz" waere gelogen).
    /// </summary>
    public static ReviewComparisonPeriods Build(int year, ReviewSpan span, DateOnly today)
    {
        // Ein abgeschlossenes (oder noch nicht begonnenes) Jahr kennt
        // keinen angebrochenen Zeitraum: "bis heute" liegt entweder hinter
        // seinem Ende oder vor seinem Anfang. Die Wahl wird dort still
        // ignoriert statt abgelehnt - die Oberflaeche bietet sie gar nicht
        // erst an, und ein Fehler waere hier keine Hilfe.
        var laufend = IstLaufendesJahr(year, today);
        var teilweise = laufend && span == ReviewSpan.GleicherZeitraum;

        var current = teilweise
            ? new DateRange(new DateOnly(year, 1, 1), today.AddDays(1))
            : DateRangePresets.Year(year);

        var previous = teilweise
            ? new DateRange(new DateOnly(year - 1, 1, 1), today.AddYears(-1).AddDays(1))
            : DateRangePresets.Year(year - 1);

        return new ReviewComparisonPeriods(
            Year: year,
            Current: current,
            Previous: previous,
            CurrentLabel: Label(year),
            PreviousLabel: Label(year - 1),
            IsPartial: teilweise,
            SpanCaption: Spanne(current));
    }

    /// <summary>Die Jahreszahl als Spaltenkopf, z. B. "2026".</summary>
    public static string Label(int year) => year.ToString("D4", CultureInfo.InvariantCulture);

    /// <summary>Ein Datum in Worten, z. B. "13. September".</summary>
    public static string TagUndMonat(DateOnly date) =>
        date.Day.ToString(CultureInfo.InvariantCulture) + ". " + MonthNames[date.Month - 1];

    /// <summary>
    /// Monat und Jahr ausgeschrieben, z. B. "September 2026". Absichtlich
    /// nicht das Kuerzel aus <see cref="ReportPeriods.Label"/>: dort ist es
    /// ein Spaltenkopf, hier steht es mitten in einem Satz.
    /// </summary>
    public static string MonatUndJahr(DateOnly date) =>
        MonthNames[date.Month - 1] + " " + Label(date.Year);

    // Beide Grenzen ausgeschrieben, damit im Hinweis ueber den Zahlen
    // nachlesbar bleibt, worauf sich der Vergleich stuetzt. Die Jahreszahl
    // fehlt hier absichtlich: die Spanne gilt fuer BEIDE Jahre, und "1.
    // Januar 2026 bis 13. September 2026" legte das Gegenteil nahe.
    private static string Spanne(DateRange range) =>
        TagUndMonat(range.From) + " bis " + TagUndMonat(range.ToExclusive.AddDays(-1));
}
