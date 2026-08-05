namespace Ausgabenverwaltung.Core.Reports;

/// <summary>
/// Zahler-Filter fuer die Auswertung. Entspricht @PayerFilter aus
/// docs/schema_v1.sql (Abfrage 2).
/// </summary>
public enum PayerScope
{
    Self,
    Others,
    All,

    /// <summary>
    /// Eigene Buchungen (unabhaengig vom Status - Regel 4) PLUS alle noch
    /// offenen Buchungen anderer PLUS zusaetzlich die bereits beglichenen
    /// Einnahmen anderer im Zeitraum: "was habe ich bisher getragen bzw.
    /// erhalten, und was kommt aus offenen Forderungen noch auf mich zu".
    /// Eine noch offene Einnahme anderer erscheint zwar (Regel aus
    /// SumCentsSql: eine Einnahme zaehlt erst nach Begleichung), traegt
    /// aber noch nichts zur Summe bei. Anders als die uebrigen drei Werte
    /// mischt dieser bewusst Zahler, Status UND Buchungstyp - siehe
    /// <see cref="ReportFilterSql.Where"/>.
    /// </summary>
    SelfAndOpen,
}
