namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Stellschrauben fuer eine Auswertung. Bewusst kein fertiger
/// Einzelreport, sondern ein Filtermodell, aus dem sich verschiedene
/// Auswertungen zusammensetzen lassen (siehe ReportRepository.Evaluate).
/// </summary>
public sealed class ReportFilter
{
    /// <summary>Zeitraumanfang, einschliesslich.</summary>
    public required DateOnly From { get; init; }

    /// <summary>Zeitraumende, ausschliesslich.</summary>
    public required DateOnly To { get; init; }

    /// <summary>
    /// Optionaler Kategorie-Knoten. Umfasst immer den Knoten selbst und
    /// alle Unterkategorien (rekursiv). NULL = keine Einschraenkung.
    /// </summary>
    public int? CategoryRootId { get; init; }

    public ReportGrouping Grouping { get; init; } = ReportGrouping.Month;

    public PayerScope PayerScope { get; init; } = PayerScope.All;

    /// <summary>
    /// Optionale Volltextsuche in der Bemerkung (Note). NULL = kein Filter.
    /// </summary>
    public string? SearchText { get; init; }
}
