namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Status-Filter fuer Ausgaben. Beachtet Regel 4: "offen" bedeutet nur in
/// Kombination mit einem fremden Zahler etwas. Eigene Ausgaben haben
/// deshalb gar keinen Status - sie zaehlen weder als offen noch als
/// beglichen und erscheinen ausschliesslich unter <see cref="Alle"/>.
///
/// Kombinierbar (<c>[Flags]</c>), damit die Filterleiste zwei unabhaengige
/// Haekchen anbieten kann statt dreier sich ausschliessender Optionen.
/// Daraus folgt eine Feinheit, die man kennen muss:
///
/// <list type="bullet">
///   <item><c>Alle</c> (nichts angehakt) heisst KEINE Einschraenkung -
///   auch eigene Ausgaben sind dabei.</item>
///   <item><c>Offene | Beglichene</c> (beides angehakt) ist NICHT dasselbe:
///   eigene Ausgaben haben keinen Status und fallen dabei heraus.</item>
/// </list>
///
/// Ein leerer Filter bedeutet also mehr Treffer als ein voller - dieselbe
/// Regel wie bei den Kategorie- und Zahler-Listen des
/// <see cref="ReportFilter"/>: eine leere Auswahl schraenkt nicht ein.
/// </summary>
[Flags]
public enum SettlementStatus
{
    /// <summary>Keine Einschraenkung - jede Buchung, mit und ohne Status.</summary>
    Alle = 0,

    /// <summary>Noch offen: kein Begleichungsdatum UND fremder Zahler.</summary>
    Offene = 1,

    /// <summary>Beglichen: Begleichungsdatum gesetzt.</summary>
    Beglichene = 2,
}
