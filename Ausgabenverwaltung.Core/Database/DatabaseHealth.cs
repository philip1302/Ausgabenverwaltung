using System.Data;
using Dapper;

namespace Ausgabenverwaltung.Core.Database;

/// <summary>
/// Integritaetspruefung der Datenbankdatei beim Start.
///
/// Verwendet <c>PRAGMA quick_check</c> und nicht <c>integrity_check</c>:
/// Letzteres prueft zusaetzlich alle Indizes gegen ihre Tabellen und
/// braucht dafuer bei jedem Start ein Vielfaches der Zeit. quick_check
/// findet die Schaeden, um die es hier geht - abgeschnittene Seiten,
/// zerstoerte Kopfdaten, eine Datei, die gar keine Datenbank ist -, und
/// zwar schnell genug, um sie bei JEDEM Start zu suchen. Eine Pruefung,
/// die zu lange dauert, um immer zu laufen, findet nichts.
/// </summary>
public static class DatabaseHealth
{
    // So viele Beanstandungen kommen in den Bericht. SQLite gibt bei einer
    // stark beschaedigten Datei hunderte Zeilen zurueck; die ersten
    // beschreiben den Schaden bereits, der Rest sind Folgefehler.
    private const int MaxReportedFindings = 5;

    /// <summary>
    /// Liefert NULL, wenn die Datei in Ordnung ist, sonst die Befunde von
    /// SQLite als Text (englisch, technisch - er gehoert ins Protokoll und
    /// in den aufklappbaren Bereich, nicht in den Haupttext einer Meldung).
    /// </summary>
    public static string? QuickCheck(IDbConnection connection)
        => Beurteile(connection.Query<string>("PRAGMA quick_check").ToList());

    /// <summary>
    /// Die gruendliche Pruefung: zusaetzlich zu allem, was
    /// <see cref="QuickCheck"/> findet, werden alle Indizes gegen ihre
    /// Tabellen gehalten. Zu langsam fuer jeden Start, aber genau richtig,
    /// wenn der Anwender EINE Sicherungsdatei ausdruecklich pruefen laesst -
    /// dort zaehlt Gruendlichkeit, nicht Tempo.
    /// </summary>
    public static string? IntegrityCheck(IDbConnection connection)
        => Beurteile(connection.Query<string>("PRAGMA integrity_check").ToList());

    // Beide Pragmas antworten in derselben Form, deshalb eine Auswertung.
    private static string? Beurteile(List<string> findings)
    {
        // Ist alles in Ordnung, liefert SQLite genau eine Zeile mit "ok".
        if (findings.Count == 1
            && string.Equals(findings[0], "ok", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Eine Datei ohne jede Beanstandung UND ohne "ok" gibt es nicht -
        // wenn doch, ist das selbst schon der Befund.
        if (findings.Count == 0)
        {
            return "Die Integritaetspruefung lieferte kein Ergebnis.";
        }

        var reported = string.Join("; ", findings.Take(MaxReportedFindings));

        return findings.Count > MaxReportedFindings
            ? $"{reported} (und {findings.Count - MaxReportedFindings} weitere Befunde)"
            : reported;
    }
}
