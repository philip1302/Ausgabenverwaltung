using System.Data;
using Microsoft.Data.Sqlite;

namespace Ausgabenverwaltung.Core.Database;

/// <summary>
/// Einzige Stelle, an der SQLite-Verbindungen geoeffnet werden. Setzt
/// bei JEDER neuen Verbindung PRAGMA foreign_keys = ON, weil SQLite das
/// sonst nicht von sich aus prueft.
/// </summary>
public static class SqliteConnectionFactory
{
    /// <summary>
    /// Wie lange auf eine gesperrte Datei gewartet wird, bevor der Zugriff
    /// als gescheitert gilt.
    ///
    /// Der Vorgabewert von Microsoft.Data.Sqlite betraegt 30 Sekunden.
    /// Fuer eine Anwendung ohne Terminal ist das die falsche Groesse: haelt
    /// ein anderes Programm die Datei fest, staende der Anwender eine halbe
    /// Minute vor einem Bildschirm ohne Fenster und ohne ein Wort - genau
    /// das, was diese Fehlerbehandlung verhindern soll. Fuenf Sekunden
    /// reichen aus, um eine kurze Sperre auszusitzen (ein
    /// Sicherungsprogramm, das die Datei gerade liest), und sind kurz
    /// genug, um danach zu erklaeren, was los ist.
    /// </summary>
    public const int BusyTimeoutSeconds = 5;

    public static IDbConnection OpenConnection(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString)
        {
            DefaultTimeout = BusyTimeoutSeconds,
        };

        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();

        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        return connection;
    }
}
