using Ausgabenverwaltung.Core.Database;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Ausgabenverwaltung.Tests;

public class DatabaseInitializerTests
{
    [Fact]
    public void Initialize_legt_alle_erwarteten_Tabellen_an()
    {
        using var connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(connection);

        var tableNames = connection.Query<string>(
            "SELECT name FROM sqlite_master WHERE type = 'table'").ToHashSet();

        Assert.Contains("Person", tableNames);
        Assert.Contains("Category", tableNames);
        Assert.Contains("RecurringExpense", tableNames);
        Assert.Contains("Expense", tableNames);
        Assert.Contains("SchemaVersion", tableNames);
    }

    [Fact]
    public void Initialize_setzt_foreign_keys_pragma()
    {
        using var connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(connection);

        var value = connection.ExecuteScalar<long>("PRAGMA foreign_keys");
        Assert.Equal(1, value);
    }

    [Fact]
    public void Initialize_schreibt_die_erwartete_SchemaVersion()
    {
        using var connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(connection);

        var version = connection.ExecuteScalar<long>("SELECT Version FROM SchemaVersion");
        Assert.Equal(2, version);
        Assert.Equal(DatabaseInitializer.ExpectedSchemaVersion, (int)version);
    }

    [Fact]
    public void Initialize_legt_die_Farbspalte_der_Kategorien_an()
    {
        using var connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(connection);

        var spalten = connection.Query<string>(
            "SELECT name FROM pragma_table_info('Category')").ToList();

        Assert.Contains("Color", spalten);
    }

    [Fact]
    public void Zweiter_Initialize_Aufruf_wirft_nicht()
    {
        using var connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(connection);
        DatabaseInitializer.Initialize(connection);

        var version = connection.ExecuteScalar<long>("SELECT COUNT(*) FROM SchemaVersion");
        Assert.Equal(1, version);
    }

    [Fact]
    public void Fremdschluessel_wird_tatsaechlich_geprueft()
    {
        using var connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(connection);

        const string sql = """
            INSERT INTO Expense (CategoryId, AmountCents, ExpenseDate, PayerId, CreatedUtc, ModifiedUtc)
            VALUES (99999, 100, '2026-01-01', 1, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z')
            """;

        Assert.Throws<SqliteException>(() => connection.Execute(sql));
    }
}
