using System.Data;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Formatting;
using Dapper;

namespace Ausgabenverwaltung.Core.People;

/// <summary>
/// Anlegen, Umbenennen und Archivieren fuer Personen. Personen werden nie
/// geloescht (Regel 8) - es gibt daher bewusst keine Delete-Methode.
/// Eindeutigkeit des Namens wird zusaetzlich zum DB-Constraint
/// (UNIQUE(Name)) hier vorab geprueft, damit Verstoesse als verstaendliche
/// DuplicatePersonNameException statt als rohe SqliteException
/// durchschlagen. Die Regel "hoechstens ein IsSelf = 1" wird weiterhin nur
/// vom DB-Constraint (UX_Person_Self) durchgesetzt und schlaegt als
/// SqliteException durch.
///
/// Die Anzeigereihenfolge ist SortOrder, nicht der Name: eine neu
/// angelegte Person haengt sich hinten an, MoveUp/MoveDown vertauschen sie
/// mit ihrer Nachbarin - genau wie bei Kategorien
/// (<see cref="Categories.CategoryRepository"/>).
/// </summary>
public sealed class PersonRepository
{
    private readonly IDbConnection _connection;

    public PersonRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public Person Create(string name, bool isSelf = false)
    {
        if (NameExists(name, excludingId: null))
        {
            throw new DuplicatePersonNameException(name);
        }

        var createdUtc = DateTime.UtcNow;
        var sortOrder = GetNextSortOrder();

        const string insertSql = """
            INSERT INTO Person (Name, IsSelf, IsArchived, CreatedUtc, SortOrder)
            VALUES (@Name, @IsSelf, 0, @CreatedUtcText, @SortOrder)
            """;

        _connection.Execute(insertSql, new
        {
            Name = name,
            IsSelf = isSelf,
            CreatedUtcText = IsoDateTime.ToUtcText(createdUtc),
            SortOrder = sortOrder,
        });

        var id = _connection.ExecuteScalar<long>("SELECT last_insert_rowid()");

        return new Person
        {
            Id = (int)id,
            Name = name,
            IsSelf = isSelf,
            IsArchived = false,
            SortOrder = sortOrder,
            CreatedUtc = createdUtc,
        };
    }

    public void Rename(int id, string newName)
    {
        if (NameExists(newName, excludingId: id))
        {
            throw new DuplicatePersonNameException(newName);
        }

        const string sql = "UPDATE Person SET Name = @Name WHERE Id = @Id";
        _connection.Execute(sql, new { Id = id, Name = newName });
    }

    public void Archive(int id)
    {
        const string sql = "UPDATE Person SET IsArchived = 1 WHERE Id = @Id";
        _connection.Execute(sql, new { Id = id });
    }

    public void Restore(int id)
    {
        const string sql = "UPDATE Person SET IsArchived = 0 WHERE Id = @Id";
        _connection.Execute(sql, new { Id = id });
    }

    public void MoveUp(int id) => Reorder(id, delta: -1);

    public void MoveDown(int id) => Reorder(id, delta: +1);

    /// <summary>
    /// Vertauscht die SortOrder mit der vorherigen (delta -1) bzw.
    /// naechsten (delta +1) Person in der Anzeigereihenfolge. Steht die
    /// Person bereits am Anfang/Ende, passiert nichts. Anders als bei
    /// Kategorien gibt es hier keine Elterngruppe - alle Personen bilden
    /// eine einzige Reihenfolge.
    /// </summary>
    private void Reorder(int id, int delta)
    {
        const string allSql = """
            SELECT Id, SortOrder
            FROM Person
            ORDER BY SortOrder, Name
            """;
        var alle = _connection.Query<SortOrderRow>(allSql).ToList();

        var index = alle.FindIndex(p => p.Id == id);
        var nachbarIndex = index + delta;
        if (index < 0 || nachbarIndex < 0 || nachbarIndex >= alle.Count)
        {
            return;
        }

        var selbst = alle[index];
        var nachbar = alle[nachbarIndex];

        const string updateSql = "UPDATE Person SET SortOrder = @SortOrder WHERE Id = @Id";
        _connection.Execute(updateSql, new { Id = selbst.Id, SortOrder = nachbar.SortOrder });
        _connection.Execute(updateSql, new { Id = nachbar.Id, SortOrder = selbst.SortOrder });
    }

    /// <summary>
    /// Anzahl der Ausgaben je Person als Zahler, fuer die Anzeige in der
    /// Personenverwaltung. Personen ohne Ausgabe fehlen im Ergebnis.
    /// </summary>
    public IReadOnlyDictionary<int, int> GetExpenseCounts()
    {
        const string sql = """
            SELECT PayerId, COUNT(*) AS Anzahl
            FROM Expense
            GROUP BY PayerId
            """;

        return _connection.Query<PersonExpenseCountRow>(sql)
            .ToDictionary(row => row.PayerId, row => row.Anzahl);
    }

    public IReadOnlyList<Person> GetAll()
    {
        const string sql = """
            SELECT Id, Name, IsSelf, IsArchived, CreatedUtc, SortOrder
            FROM Person
            ORDER BY SortOrder, Name
            """;

        return _connection.Query<PersonRow>(sql).Select(ToPerson).ToList();
    }

    /// <summary>
    /// Fuer Auswahllisten (z. B. Zahler in der Erfassungsmaske): nur
    /// nicht-archivierte Personen.
    /// </summary>
    public IReadOnlyList<Person> GetAllActive()
    {
        const string sql = """
            SELECT Id, Name, IsSelf, IsArchived, CreatedUtc, SortOrder
            FROM Person
            WHERE IsArchived = 0
            ORDER BY SortOrder, Name
            """;

        return _connection.Query<PersonRow>(sql).Select(ToPerson).ToList();
    }

    private bool NameExists(string name, int? excludingId)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM Person
            WHERE Name = @Name
              AND (@ExcludingId IS NULL OR Id <> @ExcludingId)
            """;

        var count = _connection.ExecuteScalar<int>(sql, new { Name = name, ExcludingId = excludingId });
        return count > 0;
    }

    private int GetNextSortOrder()
    {
        const string sql = "SELECT COALESCE(MAX(SortOrder), -1) + 1 FROM Person";
        return _connection.ExecuteScalar<int>(sql);
    }

    private static Person ToPerson(PersonRow row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        IsSelf = row.IsSelf,
        IsArchived = row.IsArchived,
        SortOrder = row.SortOrder,
        CreatedUtc = IsoDateTime.ParseUtc(row.CreatedUtc),
    };

    // CreatedUtc wird als reiner TEXT gelesen statt ueber automatische
    // Dapper/DateTime-Konvertierung, damit das Parsen des Formats
    // 'YYYY-MM-DDTHH:MM:SSZ' zentral ueber IsoDateTime laeuft.
    private sealed class PersonRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsSelf { get; set; }
        public bool IsArchived { get; set; }
        public int SortOrder { get; set; }
        public string CreatedUtc { get; set; } = string.Empty;
    }

    private sealed class PersonExpenseCountRow
    {
        public int PayerId { get; set; }
        public int Anzahl { get; set; }
    }

    private sealed class SortOrderRow
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
    }
}
