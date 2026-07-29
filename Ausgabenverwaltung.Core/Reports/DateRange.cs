namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Ein Zeitraum mit einschliessendem Anfang und AUSschliessendem Ende -
/// dieselbe Semantik wie <see cref="ReportFilter.From"/> /
/// <see cref="ReportFilter.To"/>, damit ein Bereich ohne weitere
/// Umrechnung in einen Filter uebernommen werden kann.
/// </summary>
public sealed record DateRange(DateOnly From, DateOnly ToExclusive)
{
    /// <summary>
    /// Ob der Zeitraum keinen einzigen Tag umfasst - etwa weil in der
    /// Filterleiste ein Bis-Datum vor dem Von-Datum steht.
    /// </summary>
    public bool IsEmpty => ToExclusive <= From;
}
