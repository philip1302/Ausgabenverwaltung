namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Eine Zeile des SQL-Ergebnisses der Kreuztabelle: Summe und Anzahl fuer
/// EINE Kategorie in EINEM Zeitabschnitt.
///
/// Die Werte enthalten bereits alle Unterkategorien - dafuer sorgt die
/// Abfrage selbst (siehe <see cref="ReportRepository.EvaluateMatrix"/>),
/// im Speicher wird nichts mehr nachaddiert.
/// </summary>
public sealed class ReportMatrixCell
{
    public required int CategoryId { get; init; }

    /// <summary>
    /// Schluessel des Zeitabschnitts, Format je nach Gruppierung "2026",
    /// "2026-Q1" oder "2026-03" (siehe <see cref="ReportPeriods"/>).
    /// </summary>
    public required string GroupKey { get; init; }

    public required long SumCents { get; init; }
    public required int Count { get; init; }
}
