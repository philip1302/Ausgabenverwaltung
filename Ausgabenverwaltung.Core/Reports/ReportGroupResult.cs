namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Eine Gruppenzeile einer Auswertung: Schluessel (abhaengig von
/// ReportFilter.Grouping, z. B. "2026", "2026-Q1" oder "2026-03"),
/// Summe in Cent und Anzahl der Ausgaben in dieser Gruppe.
/// </summary>
public sealed class ReportGroupResult
{
    public string GroupKey { get; init; } = string.Empty;
    public long SumCents { get; init; }
    public int Count { get; init; }
}
