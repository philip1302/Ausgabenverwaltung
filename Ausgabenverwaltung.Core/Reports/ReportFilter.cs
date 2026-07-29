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
    /// Optionaler einzelner Zahler. NULL = keine Einschraenkung. Wirkt
    /// zusaetzlich zu <see cref="PayerScope"/> - beide Bedingungen muessen
    /// erfuellt sein.
    /// </summary>
    public int? PayerId { get; init; }

    /// <summary>
    /// Einschraenkung auf offene bzw. beglichene Posten. Beachtet Regel 4
    /// (siehe <see cref="SettlementStatus"/>).
    /// </summary>
    public SettlementStatus Status { get; init; } = SettlementStatus.Alle;

    /// <summary>
    /// Optionale Volltextsuche in der Bemerkung (Note). NULL = kein Filter.
    /// </summary>
    public string? SearchText { get; init; }
}
