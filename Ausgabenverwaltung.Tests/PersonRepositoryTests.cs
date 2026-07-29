using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.People;
using Microsoft.Data.Sqlite;

namespace Ausgabenverwaltung.Tests;

public class PersonRepositoryTests : IDisposable
{
    private readonly System.Data.IDbConnection _connection;
    private readonly PersonRepository _repository;

    public PersonRepositoryTests()
    {
        _connection = SqliteConnectionFactory.OpenConnection("Data Source=:memory:");
        DatabaseInitializer.Initialize(_connection);
        _repository = new PersonRepository(_connection);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Create_legt_Person_an()
    {
        var person = _repository.Create("Anna");

        Assert.True(person.Id > 0);
        Assert.Equal("Anna", person.Name);
        Assert.False(person.IsSelf);
        Assert.False(person.IsArchived);
    }

    [Fact]
    public void Create_legt_Self_Person_an()
    {
        var person = _repository.Create("Ich", isSelf: true);

        Assert.True(person.IsSelf);
    }

    [Fact]
    public void Create_zweite_Self_Person_wirft()
    {
        _repository.Create("Ich", isSelf: true);

        Assert.Throws<SqliteException>(() => _repository.Create("Ich auch", isSelf: true));
    }

    [Fact]
    public void Create_mit_doppeltem_Namen_wirft()
    {
        _repository.Create("Anna");

        Assert.Throws<SqliteException>(() => _repository.Create("Anna"));
    }

    [Fact]
    public void Rename_aendert_den_Namen()
    {
        var person = _repository.Create("Anna");

        _repository.Rename(person.Id, "Anna Neu");

        var loaded = _repository.GetAll().Single(p => p.Id == person.Id);
        Assert.Equal("Anna Neu", loaded.Name);
    }

    [Fact]
    public void Archive_setzt_IsArchived_ohne_die_Zeile_zu_loeschen()
    {
        var person = _repository.Create("Anna");

        _repository.Archive(person.Id);

        var loaded = _repository.GetAll().Single(p => p.Id == person.Id);
        Assert.True(loaded.IsArchived);
    }

    [Fact]
    public void GetAll_liefert_Personen_alphabetisch_sortiert()
    {
        _repository.Create("Bernd");
        _repository.Create("Anna");

        var names = _repository.GetAll().Select(p => p.Name).ToList();

        Assert.Equal(new[] { "Anna", "Bernd" }, names);
    }

    [Fact]
    public void GetAllActive_liefert_nur_nicht_archivierte_Personen()
    {
        var anna = _repository.Create("Anna");
        _repository.Create("Bernd");
        _repository.Archive(anna.Id);

        var names = _repository.GetAllActive().Select(p => p.Name).ToList();

        Assert.Equal(new[] { "Bernd" }, names);
    }
}
