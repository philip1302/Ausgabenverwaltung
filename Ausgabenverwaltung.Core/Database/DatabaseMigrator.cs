using System.Data;
using Dapper;

namespace Ausgabenverwaltung.Core.Database;

/// <summary>
/// Zieht eine vorhandene Datenbank auf den Stand hoch, den dieser
/// Programmcode erwartet (<see cref="DatabaseInitializer.ExpectedSchemaVersion"/>).
///
/// Jeder Schritt ist eine eigene SQL-Datei in docs/, die die Spalten
/// aendert UND die neue Zeile in SchemaVersion schreibt - beides gehoert
/// zusammen und darf nicht auseinanderfallen. Ausgefuehrt wird jeder
/// Schritt in einer eigenen Transaktion: SQLite nimmt auch DDL zurueck,
/// eine abgebrochene Migration hinterlaesst also entweder den alten oder
/// den neuen Stand, nie einen halben.
///
/// Wer die Migration anstoesst und dass vorher gesichert wird, entscheidet
/// <see cref="Startup.StartupService"/> - dieser Dienst sichert nicht
/// selbst.
/// </summary>
public static class DatabaseMigrator
{
    // Von welchem Stand aus welche Datei den naechsten Schritt macht.
    // Die Reihenfolge ist die Ausfuehrungsreihenfolge.
    private static readonly (int FromVersion, string ScriptFileName)[] Migrations =
    [
        (1, "migration_v1_to_v2.sql"),
    ];

    /// <summary>
    /// Fuehrt alle noch fehlenden Schritte aus und liefert den erreichten
    /// Stand. Ist nichts zu tun, wird nichts ausgefuehrt und der
    /// vorhandene Stand zurueckgegeben.
    /// </summary>
    public static int MigrateToLatest(IDbConnection connection)
    {
        var version = DatabaseInitializer.GetSchemaVersion(connection);

        foreach (var (fromVersion, scriptFileName) in Migrations)
        {
            if (fromVersion != version)
            {
                continue;
            }

            var script = DatabaseInitializer.LoadScript(scriptFileName);

            using (var transaction = connection.BeginTransaction())
            {
                connection.Execute(script, transaction: transaction);
                transaction.Commit();
            }

            version = DatabaseInitializer.GetSchemaVersion(connection);
        }

        return version;
    }
}
