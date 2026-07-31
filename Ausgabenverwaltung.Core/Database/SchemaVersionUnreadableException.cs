namespace Ausgabenverwaltung.Core.Database;

/// <summary>
/// Wird beim Start geworfen, wenn die Tabelle SchemaVersion zwar da ist,
/// aber keinen brauchbaren Stand enthaelt - typischerweise nach einem
/// mittendrin abgebrochenen ersten Start, bei dem das Schema-Skript nur
/// halb durchlief.
///
/// Ohne diese Pruefung liefe die Anwendung mit dem Stand 0 weiter: keine
/// Migration passt darauf, die Umstellung faende also nicht statt, und der
/// Fehler taeuchte erst beim ersten Zugriff auf eine fehlende Spalte
/// irgendwo mitten in der Anwendung auf - weit weg von seiner Ursache.
/// </summary>
public sealed class SchemaVersionUnreadableException : Exception
{
    public SchemaVersionUnreadableException()
        : base(
            "Der Aufbau der Datenbank laesst sich nicht bestimmen: die Tabelle "
            + "SchemaVersion enthaelt keinen gueltigen Stand.")
    {
    }
}
