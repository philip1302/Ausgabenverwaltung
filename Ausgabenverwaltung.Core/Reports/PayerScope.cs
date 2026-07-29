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
}
