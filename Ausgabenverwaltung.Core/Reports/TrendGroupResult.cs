namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Eine Zeile aus <see cref="ReportRepository.EvaluateTrend"/>: ein
/// Zeitabschnitt, aufgeschluesselt in die drei Betraege, die das
/// Diagramm der Startseite getrennt darstellt.
///
/// Alle drei sind BETRAEGE OHNE VORZEICHEN - anders als bei
/// <see cref="ReportGroupResult.SumCents"/>, wo Ausgaben negativ
/// eingehen. Hier steht in jeder Spalte, wie viel von einer Sorte
/// zusammengekommen ist; ob das die Lage verbessert oder verschlechtert,
/// entscheidet erst die Rechnung darueber (siehe
/// <see cref="Charts.PeriodValue.NetCents"/>).
/// </summary>
public sealed class TrendGroupResult
{
    /// <summary>Abschnittsschluessel im Format von
    /// <see cref="ReportPeriods.Key"/>, z. B. "2026-03".</summary>
    public required string GroupKey { get; init; }

    /// <summary>Ausgaben, bei denen der Anwender selbst Zahler ist.</summary>
    public required long OwnExpenseCents { get; init; }

    /// <summary>
    /// Ausgaben mit fremdem Zahler, die noch offen sind - der Anwender
    /// traegt sie bis zur Abrechnung.
    /// </summary>
    public required long ForeignOpenExpenseCents { get; init; }

    /// <summary>
    /// Beglichene Einnahmen mit fremdem Zahler - tatsaechlich
    /// zugeflossenes Geld.
    /// </summary>
    public required long IncomeCents { get; init; }
}
