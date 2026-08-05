using System.Data;
using Ausgabenverwaltung.Core.Logging;
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
        (2, "migration_v2_to_v3.sql"),
    ];

    /// <summary>
    /// Fuehrt alle noch fehlenden Schritte aus und liefert den erreichten
    /// Stand. Ist nichts zu tun, wird nichts ausgefuehrt und der
    /// vorhandene Stand zurueckgegeben.
    ///
    /// Scheitert ein Schritt, wird seine Transaktion zurueckgerollt und
    /// die urspruengliche Ausnahme weitergereicht. Bereits abgeschlossene
    /// Schritte bleiben stehen - sie sind fuer sich genommen vollstaendig
    /// und in SchemaVersion vermerkt, ein Zuruecknehmen "auf Verdacht"
    /// wuerde nur einen zweiten Weg schaffen, auf dem etwas schiefgehen
    /// kann.
    /// </summary>
    public static int MigrateToLatest(IDbConnection connection)
        => MigrateToLatest(connection, Migrations, DatabaseInitializer.LoadScript);

    /// <summary>
    /// Dieselbe Ausfuehrung mit einer eigenen Schrittliste und einer
    /// eigenen Quelle fuer die Skripte.
    ///
    /// Es gibt sie fuer die Tests: dass eine abgebrochene Umstellung die
    /// Datenbank unveraendert zuruecklaesst, ist die eine Zusage, die sich
    /// nicht durch Nachdenken belegen laesst - sie muss an einem Schritt
    /// vorgefuehrt werden, der tatsaechlich scheitert. Mit den echten
    /// Migrationen ginge das nur, indem man eine davon absichtlich kaputt
    /// macht.
    /// </summary>
    public static int MigrateToLatest(
        IDbConnection connection,
        IReadOnlyList<(int FromVersion, string ScriptFileName)> migrations,
        Func<string, string> loadScript)
    {
        var version = DatabaseInitializer.GetSchemaVersion(connection);

        foreach (var (fromVersion, scriptFileName) in migrations)
        {
            if (fromVersion != version)
            {
                continue;
            }

            var script = loadScript(scriptFileName);
            var nextVersion = fromVersion + 1;

            AppLog.Current.Info(LogEvents.MigrationStarted(fromVersion, nextVersion));

            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    connection.Execute(script, transaction: transaction);
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    // Ausdruecklich zuruecknehmen statt sich auf Dispose zu
                    // verlassen: der Rueckweg ist hier die ganze Zusage, die
                    // dem Anwender gemacht wird ("die Daten sind
                    // unveraendert"), und der soll im Code stehen und nicht
                    // in einer Nebenwirkung.
                    RollbackQuietly(transaction);

                    AppLog.Current.Info(LogEvents.MigrationRolledBack(fromVersion, nextVersion));
                    AppLog.Current.Exception(
                        $"Schema-Migration {fromVersion} -> {nextVersion}", ex);

                    throw;
                }
            }

            version = DatabaseInitializer.GetSchemaVersion(connection);

            AppLog.Current.Info(LogEvents.MigrationFinished(fromVersion, version));
        }

        return version;
    }

    // Ein gescheitertes Rollback darf die urspruengliche Ausnahme nicht
    // verdecken - die beschreibt das eigentliche Problem. SQLite hat die
    // Transaktion bei manchen Fehlern bereits selbst zurueckgenommen; das
    // Rollback wirft dann, obwohl alles in Ordnung ist.
    private static void RollbackQuietly(IDbTransaction transaction)
    {
        try
        {
            transaction.Rollback();
        }
        catch (Exception)
        {
        }
    }
}
