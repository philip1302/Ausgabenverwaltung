using System.Globalization;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Expenses;
using Ausgabenverwaltung.Core.Logging;
using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.Core.YearInReview;

/// <summary>Alles, was die Seite "Jahresrueckblick" anzeigt.</summary>
public sealed record YearInReviewResult(
    ReviewComparisonPeriods Periods,
    ReviewHeadline Headline,
    ReviewComparison Comparison,
    IReadOnlyList<ReviewFinding> Findings,
    ReviewDataState State,
    IReadOnlyList<ReviewMonthPair> Months);

/// <summary>
/// Stellt den Jahresrueckblick zusammen. Fuehrt nur vorhandene Bausteine
/// aneinander - es gibt hier kein eigenes SQL.
///
/// <b>Zwei Abfragen statt einer</b>, je Zeitraum eine: ein einziger Aufruf
/// ueber beide Jahre koennte den Schnitt "bis heute" nicht ausdruecken, denn
/// die Zeitgrenzen eines Filters sind EIN Intervall - Januar bis September
/// 2026 und Januar bis September 2025 sind zwei getrennte. Nach Monaten zu
/// gruppieren und im Speicher aufzusummieren scheitert daran, dass der
/// Schnitt auf einem TAG liegt und nicht auf einer Monatsgrenze. Eine
/// eigene Abfrage mit zwei Zeitfenstern wuerde die Ahnen-CTE und die
/// Filterbedingungen verdoppeln, ohne etwas zu gewinnen; zwei Abfragen
/// gegen eine oertliche Datei kosten nichts.
///
/// Zusammengeschuettet werden darf das, weil jeder Zeitraum vollstaendig
/// INNERHALB eines Kalenderjahres liegt: nach Jahren gruppiert liefert
/// jede Abfrage damit genau einen Schluessel, und die beiden koennen nicht
/// kollidieren.
/// </summary>
public sealed class YearInReviewService
{
    private readonly ReportRepository _reports;
    private readonly CategoryRepository _categories;
    private readonly ExpenseRepository _expenses;

    public YearInReviewService(
        ReportRepository reports, CategoryRepository categories, ExpenseRepository expenses)
    {
        _reports = reports;
        _categories = categories;
        _expenses = expenses;
    }

    /// <summary>
    /// Die Jahre, fuer die es ueberhaupt Buchungen gibt - absteigend, das
    /// juengste zuerst. Bewusst ohne Einschraenkung auf Ausgaben: ein Jahr,
    /// in dem nur Einnahmen stehen, ist trotzdem eines zum Anschauen.
    /// </summary>
    public IReadOnlyList<int> JahreMitBuchungen()
    {
        var alles = DateRangePresets.Everything();

        return _reports
            .Evaluate(new ReportFilter
            {
                From = alles.From,
                To = alles.ToExclusive,
                Grouping = ReportGrouping.Year,
            })
            .Select(gruppe => int.Parse(gruppe.GroupKey, CultureInfo.InvariantCulture))
            .OrderByDescending(jahr => jahr)
            .ToList();
    }

    public YearInReviewResult Build(int year, ReviewSpan span, DateOnly today)
    {
        var zeitraeume = ReviewPeriods.Build(year, span, today);

        var zellen = new List<ReportMatrixCell>();
        zellen.AddRange(_reports.EvaluateMatrix(Ausgabenfilter(
            zeitraeume.Previous, ReportGrouping.Year)));
        zellen.AddRange(_reports.EvaluateMatrix(Ausgabenfilter(
            zeitraeume.Current, ReportGrouping.Year)));

        var baum = _categories.GetTree();
        var pfade = CategoryPaths.BuildFullPaths(baum);
        var matrix = ReportMatrixBuilder.Build(baum, pfade, zellen, ReportGrouping.Year);

        var vorjahrSpalte = matrix.ColumnTotal(zeitraeume.PreviousLabel);
        var jahrSpalte = matrix.ColumnTotal(zeitraeume.CurrentLabel);

        var vergleich = new ReviewComparison(
            Periods: zeitraeume,
            Roots: BaueZeilen(matrix.Rows, zeitraeume.PreviousLabel, zeitraeume.CurrentLabel),
            // Das eine Mal, an dem die Vorzeichen gedreht werden.
            PreviousTotalCents: -vorjahrSpalte.SumCents,
            CurrentTotalCents: -jahrSpalte.SumCents,
            PreviousTotalCount: vorjahrSpalte.Count,
            CurrentTotalCount: jahrSpalte.Count);

        // Die Ausgaben werden NICHT eigens abgefragt, sondern kommen aus
        // derselben Summe wie die Tabelle - so kann die Kachel oben nie von
        // der Summenzeile unten abweichen.
        var kennzahlen = ReviewHeadline.Build(
            vorjahrAusgabenCents: vergleich.PreviousTotalCents,
            jahrAusgabenCents: vergleich.CurrentTotalCents,
            vorjahrEinnahmenCents: Einnahmen(zeitraeume.Previous),
            jahrEinnahmenCents: Einnahmen(zeitraeume.Current));

        var jahrMonate = Monate(zeitraeume.Current);

        var befunde = ReviewFindings.Build(vergleich, jahrMonate);
        var lage = ReviewFindings.Bewerte(vergleich, befunde, _expenses.HasAny());

        var monate = Paare(Monate(zeitraeume.Previous), jahrMonate);

        AppLog.Current.Info(LogEvents.YearInReviewBuilt(
            year, wholeYears: !zeitraeume.IsPartial, findingCount: befunde.Count));

        return new YearInReviewResult(
            zeitraeume, kennzahlen, vergleich, befunde, lage, monate);
    }

    /// <summary>
    /// Legt die beiden Monatsreihen nebeneinander.
    ///
    /// Gepaart wird ueber die POSITION im Zeitraum und nicht ueber den
    /// Schluessel - die Schluessel tragen verschiedene Jahre und koennten
    /// sich gar nicht treffen.
    ///
    /// Beide Reihen sind stets gleich lang: <see cref="ReviewPeriods.Build"/>
    /// laesst beide Zeitraeume am 1. Januar beginnen und am selben Tag des
    /// Monats enden, im angebrochenen Jahr wie im ganzen. Sollte sich das
    /// dort je aendern, gewinnt hier die kuerzere Reihe, statt dass eine
    /// Ausnahme fliegt - ein Rueckblick, der wegen eines Randfalls gar
    /// nicht mehr aufgeht, waere schlimmer als einer, dem ein Monat fehlt.
    /// Ein Test haelt die Gleichheit fest.
    /// </summary>
    private static IReadOnlyList<ReviewMonthPair> Paare(
        IReadOnlyList<ReviewMonth> vorjahr, IReadOnlyList<ReviewMonth> jahr)
    {
        var anzahl = Math.Min(vorjahr.Count, jahr.Count);
        var paare = new List<ReviewMonthPair>(anzahl);

        for (var i = 0; i < anzahl; i++)
        {
            var beginn = ReportPeriods.Start(jahr[i].Key, ReportGrouping.Month);

            paare.Add(new ReviewMonthPair(
                Key: jahr[i].Key,
                // Nur das Monatskuerzel: die Jahreszahlen stehen in der
                // Legende, und an jeder Beschriftung wiederholt kosteten
                // sie nur Platz.
                Label: ReportPeriods.MonthAbbreviation(beginn.Month),
                PreviousCents: vorjahr[i].ExpenseCents,
                CurrentCents: jahr[i].ExpenseCents,
                CurrentCount: jahr[i].Count));
        }

        return paare;
    }

    // Kein Zahler- und kein Statusfilter: der Rueckblick sieht bewusst
    // alles, was gebucht wurde. Das ist seine Aussage - wer einzelne
    // Zahler oder offene Posten betrachten will, hat dafuer die Auswertung.
    private static ReportFilter Ausgabenfilter(DateRange zeitraum, ReportGrouping gruppierung) =>
        new()
        {
            From = zeitraum.From,
            To = zeitraum.ToExclusive,
            Grouping = gruppierung,
            IsIncome = false,
        };

    // Einnahmen kommen schon positiv aus der Auswertung; offene Einnahmen
    // zaehlen dort mit null, weil das Geld noch nicht geflossen ist.
    private long Einnahmen(DateRange zeitraum) =>
        _reports
            .Evaluate(new ReportFilter
            {
                From = zeitraum.From,
                To = zeitraum.ToExclusive,
                Grouping = ReportGrouping.Year,
                IsIncome = true,
            })
            .Sum(gruppe => gruppe.SumCents);

    // Lueckenlos, auch ueber Monate ohne jede Buchung: sonst stimmt der
    // Monatsschnitt nicht, und ein auffaellig guenstiger Monat koennte gar
    // nicht auffallen.
    private IReadOnlyList<ReviewMonth> Monate(DateRange zeitraum)
    {
        var summen = _reports
            .Evaluate(Ausgabenfilter(zeitraum, ReportGrouping.Month))
            .ToDictionary(gruppe => gruppe.GroupKey);

        var ersterKey = ReportPeriods.Key(zeitraum.From, ReportGrouping.Month);
        var letzterKey = ReportPeriods.Key(
            zeitraum.ToExclusive.AddDays(-1), ReportGrouping.Month);

        return ReportPeriods
            .Enumerate(ersterKey, letzterKey, ReportGrouping.Month)
            .Select(key =>
            {
                var beginn = ReportPeriods.Start(key, ReportGrouping.Month);
                summen.TryGetValue(key, out var gruppe);

                return new ReviewMonth(
                    Key: key,
                    Label: ReviewPeriods.MonatUndJahr(beginn),
                    ExpenseCents: gruppe is null ? 0 : -gruppe.SumCents,
                    Count: gruppe?.Count ?? 0);
            })
            .ToList();
    }

    // Kategorien ohne eine einzige Buchung in BEIDEN Jahren fallen heraus -
    // sonst bestuende die Tabelle zur Haelfte aus leeren Zeilen. Weil die
    // Werte einer Oberkategorie ihren ganzen Ast enthalten, kann unter
    // einer leeren Zeile nichts Belegtes mehr haengen.
    private static IReadOnlyList<ReviewCategoryChange> BaueZeilen(
        IReadOnlyList<ReportMatrixRow> zeilen, string vorjahrKey, string jahrKey)
    {
        var ergebnis = new List<ReviewCategoryChange>();

        foreach (var zeile in zeilen)
        {
            var vorjahr = zeile.Cell(vorjahrKey);
            var jahr = zeile.Cell(jahrKey);

            if (!vorjahr.HasValues && !jahr.HasValues)
            {
                continue;
            }

            ergebnis.Add(new ReviewCategoryChange
            {
                CategoryId = zeile.CategoryId,
                Name = zeile.Name,
                FullPath = zeile.FullPath,
                Depth = zeile.Depth,
                IsArchived = zeile.IsArchived,
                PreviousCents = -vorjahr.SumCents,
                CurrentCents = -jahr.SumCents,
                PreviousCount = vorjahr.Count,
                CurrentCount = jahr.Count,
                Children = BaueZeilen(zeile.Children, vorjahrKey, jahrKey),
            });
        }

        return ergebnis;
    }
}
