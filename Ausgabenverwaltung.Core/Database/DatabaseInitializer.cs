using System.Data;
using System.Reflection;
using Dapper;

namespace Ausgabenverwaltung.Core.Database;

/// <summary>
/// Fuehrt das massgebliche Schema aus docs/schema_v1.sql gegen eine
/// geoeffnete Verbindung aus.
/// </summary>
public static class DatabaseInitializer
{
    // Ab hier folgen in schema_v1.sql nur noch Beispiel-Abfragen mit
    // ungebundenen Parametern (@RootId, @Von, ...) - die sind nicht Teil
    // des auszufuehrenden Schemas und wuerden beim Ausfuehren fehlschlagen.
    private const string ExampleQueriesMarker = "-- ABFRAGE 1";

    // Von schema_v1.sql eingetragene SchemaVersion. Steigt mit jeder
    // Migration, die der Code beherrscht.
    public const int ExpectedSchemaVersion = 1;

    public static void Initialize(IDbConnection connection)
    {
        if (SchemaAlreadyApplied(connection))
        {
            return;
        }

        var script = LoadSchemaScript();
        connection.Execute(script);
    }

    // MAX() statt einer einfachen SELECT Version, damit die Abfrage auch
    // dann eindeutig bleibt, wenn kuenftige Migrationen mehrere Zeilen in
    // SchemaVersion hinterlassen.
    public static int GetSchemaVersion(IDbConnection connection)
    {
        const string sql = "SELECT MAX(Version) FROM SchemaVersion";
        return connection.ExecuteScalar<int>(sql);
    }

    private static bool SchemaAlreadyApplied(IDbConnection connection)
    {
        const string sql = """
            SELECT name FROM sqlite_master
            WHERE type = 'table' AND name = 'SchemaVersion'
            """;

        return connection.QueryFirstOrDefault<string>(sql) is not null;
    }

    private static string LoadSchemaScript()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("schema_v1.sql", StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Eingebettete Ressource '{resourceName}' nicht gefunden.");
        using var reader = new StreamReader(stream);
        var fullScript = reader.ReadToEnd();

        var markerIndex = fullScript.IndexOf(ExampleQueriesMarker, StringComparison.Ordinal);
        return markerIndex < 0 ? fullScript : fullScript[..markerIndex];
    }
}
