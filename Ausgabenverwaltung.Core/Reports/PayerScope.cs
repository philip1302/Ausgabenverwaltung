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
    /// Eigene Ausgaben (unabhaengig vom Status - Regel 4) PLUS die noch
    /// nicht beglichenen Ausgaben anderer im Zeitraum: "was habe ich
    /// bisher getragen, und was kommt aus offenen Forderungen noch auf
    /// mich zu". Anders als die uebrigen drei Werte mischt dieser bewusst
    /// den Zahler- mit dem Status-Filter - siehe
    /// <see cref="ReportFilterSql.Where"/>.
    /// </summary>
    SelfAndOpen,
}
