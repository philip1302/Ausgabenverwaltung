namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Status-Filter fuer Ausgaben. Beachtet Regel 4: "offen" bedeutet nur in
/// Kombination mit einem fremden Zahler etwas. Eigene Ausgaben haben
/// deshalb gar keinen Status und erscheinen ausschliesslich unter
/// <see cref="Alle"/> - sie zaehlen weder als offen noch als beglichen.
/// </summary>
public enum SettlementStatus
{
    Alle,
    NurOffene,
    NurBeglichene,
}
