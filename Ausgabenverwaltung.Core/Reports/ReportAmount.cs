namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Summe und Trefferzahl einer Zelle der Kreuztabelle.
///
/// Die Anzahl steht bewusst neben der Summe: eine Zelle mit Summe 0 -
/// etwa Ausgabe und Erstattung im selben Zeitabschnitt - ist etwas
/// anderes als ein Zeitabschnitt ohne jede Buchung. Nur letzterer wird in
/// der Anzeige als Bindestrich dargestellt, und allein an der Anzahl
/// laesst sich das unterscheiden.
/// </summary>
public readonly record struct ReportAmount(long SumCents, int Count)
{
    public static ReportAmount Empty => new(0, 0);

    /// <summary>Ob in dieser Zelle ueberhaupt Buchungen liegen.</summary>
    public bool HasValues => Count > 0;

    public ReportAmount Add(ReportAmount other) =>
        new(SumCents + other.SumCents, Count + other.Count);
}
