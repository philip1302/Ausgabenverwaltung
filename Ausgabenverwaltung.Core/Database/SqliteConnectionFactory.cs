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
    public static IDbConnection OpenConnection(string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();

        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        return connection;
    }
}
