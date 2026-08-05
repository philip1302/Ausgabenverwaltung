using System.Data;
using System.Reflection;
using Dapper;

namespace Ausgabenverwaltung.Core.Database;

/// <summary>
/// Legt neue Datenbanken nach dem massgeblichen Schema an (docs/schema_v4.sql)
/// und liest den Stand einer vorhandenen. Bestehende Datenbanken aelterer
/// Staende zieht <see cref="DatabaseMigrator"/> hoch.
/// </summary>
public static class DatabaseInitializer
{
    // Ab hier folgen in den Schema-Dateien nur noch Beispiel-Abfragen mit
    // ungebundenen Parametern (@RootId, @Von, ...) - die sind nicht Teil
    // des auszufuehrenden Schemas und wuerden beim Ausfuehren fehlschlagen.
    private const string ExampleQueriesMarker = "-- ABFRAGE 1";

    /// <summary>Schema-Datei, aus der neue Datenbanken entstehen.</summary>
    public const string CurrentSchemaFileName = "schema_v4.sql";

    // Stand, den dieser Programmcode erwartet. Steigt mit jeder Migration,
    // die er beherrscht (siehe DatabaseMigrator.Migrations).
    public const int ExpectedSchemaVersion = 4;

    public static void Initialize(IDbConnection connection)
    {
        if (SchemaAlreadyApplied(connection))
        {
            return;
        }

        connection.Execute(LoadScript(CurrentSchemaFileName));
    }

    /// <summary>
    /// Der Stand der Datenbank. Wirft
    /// <see cref="SchemaVersionUnreadableException"/>, wenn sich kein
    /// Stand bestimmen laesst - siehe <see cref="ReadSchemaVersion"/>.
    /// </summary>
    public static int GetSchemaVersion(IDbConnection connection)
        => ReadSchemaVersion(connection) ?? throw new SchemaVersionUnreadableException();

    /// <summary>
    /// Der Stand der Datenbank, oder NULL, wenn keiner darin steht.
    ///
    /// MAX() statt einer einfachen SELECT Version, weil jede Migration eine
    /// weitere Zeile in SchemaVersion hinterlaesst. Auf einer leeren
    /// Tabelle liefert MAX() allerdings NULL, und das kaeme als 0 heraus -
    /// ein Stand, den es nie gab und auf den keine Migration passt. Diese
    /// Ueberladung macht den Unterschied sichtbar, statt ihn in eine 0 zu
    /// verwandeln.
    /// </summary>
    public static int? ReadSchemaVersion(IDbConnection connection)
    {
        const string sql = "SELECT MAX(Version) FROM SchemaVersion";
        var version = connection.ExecuteScalar<int?>(sql);

        // Ein Stand kleiner als 1 ist genauso wenig zu gebrauchen wie gar
        // keiner: die erste Schema-Datei traegt Version 1 ein.
        return version is null or < 1 ? null : version;
    }

    /// <summary>
    /// Laedt eine der eingebetteten SQL-Dateien und schneidet die
    /// Beispiel-Abfragen am Ende ab. Oeffentlich, damit die Tests eine
    /// Datenbank im Stand der Version 1 aus dem echten alten Schema
    /// anlegen koennen statt aus einer Nachbildung.
    /// </summary>
    public static string LoadScript(string resourceFileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(resourceFileName, StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Eingebettete Ressource '{resourceName}' nicht gefunden.");
        using var reader = new StreamReader(stream);
        var fullScript = reader.ReadToEnd();

        var markerIndex = fullScript.IndexOf(ExampleQueriesMarker, StringComparison.Ordinal);
        return markerIndex < 0 ? fullScript : fullScript[..markerIndex];
    }

    private static bool SchemaAlreadyApplied(IDbConnection connection)
    {
        const string sql = """
            SELECT name FROM sqlite_master
            WHERE type = 'table' AND name = 'SchemaVersion'
            """;

        return connection.QueryFirstOrDefault<string>(sql) is not null;
    }
}
