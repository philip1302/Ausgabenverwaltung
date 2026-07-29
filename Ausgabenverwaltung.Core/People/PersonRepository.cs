using System.Data;
using Ausgabenverwaltung.Core.Entities;
using Ausgabenverwaltung.Core.Formatting;
using Dapper;

namespace Ausgabenverwaltung.Core.People;

/// <summary>
/// Anlegen, Umbenennen und Archivieren fuer Personen. Personen werden nie
/// geloescht (Regel 8) - es gibt daher bewusst keine Delete-Methode.
/// Eindeutigkeit des Namens und die Regel "hoechstens ein IsSelf = 1"
/// werden von den DB-Constraints durchgesetzt (UNIQUE(Name),
/// UX_Person_Self); Verstoesse schlagen als SqliteException durch.
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
        var createdUtc = DateTime.UtcNow;

        const string insertSql = """
            INSERT INTO Person (Name, IsSelf, IsArchived, CreatedUtc)
            VALUES (@Name, @IsSelf, 0, @CreatedUtcText)
            """;

        _connection.Execute(insertSql, new
        {
            Name = name,
            IsSelf = isSelf,
            CreatedUtcText = IsoDateTime.ToUtcText(createdUtc),
        });

        var id = _connection.ExecuteScalar<long>("SELECT last_insert_rowid()");

        return new Person
        {
            Id = (int)id,
            Name = name,
            IsSelf = isSelf,
            IsArchived = false,
            CreatedUtc = createdUtc,
        };
    }

    public void Rename(int id, string newName)
    {
        const string sql = "UPDATE Person SET Name = @Name WHERE Id = @Id";
        _connection.Execute(sql, new { Id = id, Name = newName });
    }

    public void Archive(int id)
    {
        const string sql = "UPDATE Person SET IsArchived = 1 WHERE Id = @Id";
        _connection.Execute(sql, new { Id = id });
    }

    public IReadOnlyList<Person> GetAll()
    {
        const string sql = """
            SELECT Id, Name, IsSelf, IsArchived, CreatedUtc
            FROM Person
            ORDER BY Name
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
            SELECT Id, Name, IsSelf, IsArchived, CreatedUtc
            FROM Person
            WHERE IsArchived = 0
            ORDER BY Name
            """;

        return _connection.Query<PersonRow>(sql).Select(ToPerson).ToList();
    }

    private static Person ToPerson(PersonRow row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        IsSelf = row.IsSelf,
        IsArchived = row.IsArchived,
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
        public string CreatedUtc { get; set; } = string.Empty;
    }
}
