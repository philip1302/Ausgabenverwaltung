using Ausgabenverwaltung.Core.Reports;

namespace Ausgabenverwaltung.Core.YearInReview;

/// <summary>
/// Die beiden Zeitraeume, die der Jahresrueckblick gegenueberstellt, samt
/// ihrer Beschriftung. Erzeugt wird das ausschliesslich von
/// <see cref="ReviewPeriods.Build"/>.
///
/// <see cref="Current"/> und <see cref="Previous"/> liegen jeweils
/// vollstaendig INNERHALB eines Kalenderjahres. Darauf beruht der Aufbau
/// des Rueckblicks: eine Abfrage je Zeitraum mit
/// <see cref="ReportGrouping.Year"/> liefert deshalb genau eine Spalte, und
/// die beiden Ergebnisse lassen sich ohne Schluesselkollision
/// zusammenschuetten.
/// </summary>
public sealed record ReviewComparisonPeriods(
    int Year,
    DateRange Current,
    DateRange Previous,
    string CurrentLabel,
    string PreviousLabel,
    bool IsPartial,
    string SpanCaption)
{
    /// <summary>
    /// Der letzte Tag des betrachteten Jahres - EINschliessend, also
    /// unmittelbar anzeigbar. <see cref="DateRange.ToExclusive"/> waere der
    /// Tag danach und in einem Satz wie "bis 13. September" falsch.
    /// </summary>
    public DateOnly CurrentLastDay => Current.ToExclusive.AddDays(-1);

    /// <summary>Der letzte Tag des Vorjahreszeitraums, ebenfalls einschliessend.</summary>
    public DateOnly PreviousLastDay => Previous.ToExclusive.AddDays(-1);
}
