using System.Data;
using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Dapper;

namespace Ausgabenverwaltung.Tests;

/// <summary>
/// Die Migration ist der einzige Vorgang, der eine gefuellte Datenbank
/// baulich veraendert. Geprueft wird deshalb nicht nur, dass sie
/// durchlaeuft, sondern dass hinterher noch alles da ist.
/// </summary>
public class DatabaseMigratorTests : IDisposable
{
    private readonly IDbConnection _connection =
        SqliteConnectionFactory.OpenConnection("Data Source=:memory:");

    public void Dispose() => _connection.Dispose();

    // Legt eine Datenbank im Stand der Version 1 an - aus dem echten
    // alten Schema, nicht aus einer Nachbildung.
    private void LegeVersion1An()
    {
        _connection.Execute(DatabaseInitializer.LoadScript("schema_v1.sql"));
    }

    private static IReadOnlyList<string> Spaltennamen(IDbConnection connection, string tabelle)
        => connection.Query<string>($"SELECT name FROM pragma_table_info('{tabelle}')").ToList();

    [Fact]
    public void Version_1_wird_in_einem_Rutsch_auf_die_aktuelle_Version_gehoben()
    {
        LegeVersion1An();
        Assert.Equal(1, DatabaseInitializer.GetSchemaVersion(_connection));

        var version = DatabaseMigrator.MigrateToLatest(_connection);

        Assert.Equal(DatabaseInitializer.ExpectedSchemaVersion, version);
        Assert.Equal(DatabaseInitializer.ExpectedSchemaVersion, DatabaseInitializer.GetSchemaVersion(_connection));
    }

    [Fact]
    public void Migration_erhaelt_alle_Daten()
    {
        LegeVersion1An();

        // Ein Stand, der durch alle vier Tabellen geht: Person, ein
        // Kategoriebaum, eine Vorlage und zwei Buchungen - eine davon aus
        // der Vorlage erzeugt und offen.
        _connection.Execute("""
            INSERT INTO Person (Id, Name, IsSelf, IsArchived, CreatedUtc)
            VALUES (1, 'Ich', 1, 0, '2026-01-01T00:00:00Z'),
                   (2, 'Anna', 0, 0, '2026-01-02T00:00:00Z');

            INSERT INTO Category (Id, ParentId, Name, SortOrder, IsArchived, CreatedUtc)
            VALUES (1, NULL, 'Pferde', 0, 0, '2026-01-01T00:00:00Z'),
                   (2, 1,    'Hufschmied', 0, 0, '2026-01-01T00:00:00Z');

            INSERT INTO RecurringExpense
                (Id, CategoryId, PayerId, AmountCents, Title, IntervalUnit,
                 IntervalCount, AnchorDay, StartDate, GeneratedThrough,
                 IsActive, CreatedUtc, ModifiedUtc)
            VALUES (1, 2, 1, 12000, 'Stallmiete', 'month', 1, 1,
                    '2026-01-01', '2026-02-01', 1,
                    '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');

            INSERT INTO Expense
                (Id, CategoryId, AmountCents, ExpenseDate, Note, PayerId,
                 SettledDate, RecurringExpenseId, CreatedUtc, ModifiedUtc)
            VALUES (1, 2, 12000, '2026-01-01', 'Beschlag', 1,
                    NULL, 1, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z'),
                   (2, 1, -2550, '2026-02-03', 'Erstattung', 2,
                    NULL, NULL, '2026-02-03T00:00:00Z', '2026-02-03T00:00:00Z');
            """);

        DatabaseMigrator.MigrateToLatest(_connection);

        Assert.Equal(2, _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Person"));
        Assert.Equal(2, _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Category"));
        Assert.Equal(1, _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM RecurringExpense"));
        Assert.Equal(2, _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Expense"));

        // Stichproben auf die Werte selbst - eine Migration, die die
        // Zeilenzahl haelt und die Betraege verdreht, waere schlimmer als
        // eine, die abbricht.
        Assert.Equal("Anna", _connection.ExecuteScalar<string>(
            "SELECT Name FROM Person WHERE Id = 2"));
        Assert.Equal(-2550L, _connection.ExecuteScalar<long>(
            "SELECT AmountCents FROM Expense WHERE Id = 2"));
        Assert.Equal("2026-02-01", _connection.ExecuteScalar<string>(
            "SELECT GeneratedThrough FROM RecurringExpense WHERE Id = 1"));
        Assert.Equal(1, _connection.ExecuteScalar<int>(
            "SELECT ParentId FROM Category WHERE Id = 2"));
    }

    [Fact]
    public void Nach_der_Migration_haben_alle_Kategorien_keine_eigene_Farbe()
    {
        LegeVersion1An();
        _connection.Execute("""
            INSERT INTO Category (Id, ParentId, Name, SortOrder, IsArchived, CreatedUtc)
            VALUES (1, NULL, 'Pferde', 0, 0, '2026-01-01T00:00:00Z');
            """);

        DatabaseMigrator.MigrateToLatest(_connection);

        Assert.Contains("Color", Spaltennamen(_connection, "Category"));
        Assert.Equal(0, _connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM Category WHERE Color IS NOT NULL"));

        // Und die Vererbung liefert fuer diesen Stand den Standardwert.
        var farben = new CategoryRepository(_connection).GetResolvedColors();
        Assert.Equal(CategoryColorPalette.DefaultHex, farben[1]);
    }

    [Fact]
    public void Migration_v2_zu_v3_vergibt_SortOrder_nach_bisheriger_alphabetischer_Reihenfolge()
    {
        // Stand der Version 2 - vor Einfuehrung von Person.SortOrder gab
        // es nur die alphabetische Sortierung ueber Name. Die Migration
        // muss genau diese Reihenfolge als Startwert einfrieren, damit
        // sich fuer bereits vorhandene Personen beim ersten Start nichts
        // sichtbar aendert.
        _connection.Execute(DatabaseInitializer.LoadScript("schema_v2.sql"));
        _connection.Execute("""
            INSERT INTO Person (Id, Name, IsSelf, IsArchived, CreatedUtc)
            VALUES (1, 'Ich', 1, 0, '2026-01-01T00:00:00Z'),
                   (2, 'Zora', 0, 0, '2026-01-02T00:00:00Z'),
                   (3, 'Anna', 0, 0, '2026-01-03T00:00:00Z');
            """);

        DatabaseMigrator.MigrateToLatest(_connection);

        Assert.Contains("SortOrder", Spaltennamen(_connection, "Person"));

        var namenNachSortOrder = _connection.Query<string>(
            "SELECT Name FROM Person ORDER BY SortOrder").ToList();
        Assert.Equal(new[] { "Anna", "Ich", "Zora" }, namenNachSortOrder);
    }

    [Fact]
    public void Migration_v3_zu_v4_fuegt_IsIncome_hinzu_und_belaesst_bestehende_Buchungen_als_Ausgabe()
    {
        // Stand der Version 3 - vor Einfuehrung von IsIncome gab es nur
        // Ausgaben. ALTER TABLE ... ADD COLUMN ... DEFAULT 0 muss deshalb
        // jede bestehende Zeile unveraendert als Ausgabe stehen lassen.
        _connection.Execute(DatabaseInitializer.LoadScript("schema_v3.sql"));
        _connection.Execute("""
            INSERT INTO Person (Id, Name, IsSelf, IsArchived, CreatedUtc, SortOrder)
            VALUES (1, 'Ich', 1, 0, '2026-01-01T00:00:00Z', 0);

            INSERT INTO Category (Id, ParentId, Name, SortOrder, IsArchived, CreatedUtc)
            VALUES (1, NULL, 'Pferde', 0, 0, '2026-01-01T00:00:00Z');

            INSERT INTO RecurringExpense
                (Id, CategoryId, PayerId, AmountCents, Title, IntervalUnit,
                 IntervalCount, AnchorDay, StartDate, GeneratedThrough,
                 IsActive, CreatedUtc, ModifiedUtc)
            VALUES (1, 1, 1, 12000, 'Stallmiete', 'month', 1, 1,
                    '2026-01-01', '2026-02-01', 1,
                    '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');

            INSERT INTO Expense
                (Id, CategoryId, AmountCents, ExpenseDate, Note, PayerId,
                 SettledDate, RecurringExpenseId, CreatedUtc, ModifiedUtc)
            VALUES (1, 1, 12000, '2026-01-01', 'Beschlag', 1,
                    NULL, 1, '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z');
            """);

        DatabaseMigrator.MigrateToLatest(_connection);

        Assert.Contains("IsIncome", Spaltennamen(_connection, "Expense"));
        Assert.Contains("IsIncome", Spaltennamen(_connection, "RecurringExpense"));

        Assert.Equal(0, _connection.ExecuteScalar<int>(
            "SELECT IsIncome FROM Expense WHERE Id = 1"));
        Assert.Equal(0, _connection.ExecuteScalar<int>(
            "SELECT IsIncome FROM RecurringExpense WHERE Id = 1"));
    }

    [Fact]
    public void Migrierte_und_neu_angelegte_Datenbank_haben_dieselben_Spalten()
    {
        LegeVersion1An();
        DatabaseMigrator.MigrateToLatest(_connection);

        using var neu = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(neu);

        foreach (var tabelle in new[] { "Person", "Category", "RecurringExpense", "Expense" })
        {
            Assert.Equal(Spaltennamen(neu, tabelle), Spaltennamen(_connection, tabelle));
        }
    }

    [Fact]
    public void Migration_einer_aktuellen_Datenbank_aendert_nichts()
    {
        DatabaseInitializer.Initialize(_connection);

        var version = DatabaseMigrator.MigrateToLatest(_connection);

        Assert.Equal(DatabaseInitializer.ExpectedSchemaVersion, version);
        Assert.Equal(1, _connection.ExecuteScalar<int>("SELECT COUNT(*) FROM SchemaVersion"));
    }
}
