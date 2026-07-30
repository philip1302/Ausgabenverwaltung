namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Die fertige Kreuztabelle: Kategorien als Zeilen, Zeitabschnitte als
/// Spalten, dazu Spaltensummen und die Gesamtsumme. Enthaelt keinen
/// Anzeigezustand - was aufgeklappt oder ausgeblendet ist, entscheidet die
/// Oberflaeche, ohne die Auswertung erneut laufen zu lassen.
/// </summary>
public sealed class ReportMatrix
{
    public required ReportGrouping Grouping { get; init; }

    /// <summary>
    /// Die Spalten in chronologischer Reihenfolge, LUECKENLOS vom
    /// fruehesten bis zum spaetesten belegten Zeitabschnitt. Abschnitte
    /// ohne Buchungen stehen also mit drin.
    /// </summary>
    public required IReadOnlyList<string> PeriodKeys { get; init; }

    /// <summary>Die obersten Zeilen; Unterkategorien haengen darunter.</summary>
    public required IReadOnlyList<ReportMatrixRow> Rows { get; init; }

    public required IReadOnlyDictionary<string, ReportAmount> ColumnTotals { get; init; }

    /// <summary>Summe ueber alle Zeilen und alle Zeitabschnitte.</summary>
    public required ReportAmount Total { get; init; }

    public ReportAmount ColumnTotal(string periodKey) =>
        ColumnTotals.TryGetValue(periodKey, out var amount) ? amount : ReportAmount.Empty;

    /// <summary>Eine Auswertung ohne einen einzigen Treffer.</summary>
    public static ReportMatrix Empty(ReportGrouping grouping) => new()
    {
        Grouping = grouping,
        PeriodKeys = Array.Empty<string>(),
        Rows = Array.Empty<ReportMatrixRow>(),
        ColumnTotals = new Dictionary<string, ReportAmount>(),
        Total = ReportAmount.Empty,
    };
}
