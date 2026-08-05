using Ausgabenverwaltung.Core.Categories;
using Ausgabenverwaltung.Core.Database;
using Ausgabenverwaltung.Core.Expenses;
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

        Assert.Throws<DuplicatePersonNameException>(() => _repository.Create("Anna"));
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
    public void Rename_auf_bereits_vergebenen_Namen_wirft()
    {
        _repository.Create("Anna");
        var bernd = _repository.Create("Bernd");

        Assert.Throws<DuplicatePersonNameException>(() => _repository.Rename(bernd.Id, "Anna"));
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
    public void Restore_macht_das_Archivieren_rueckgaengig()
    {
        var person = _repository.Create("Anna");
        _repository.Archive(person.Id);

        _repository.Restore(person.Id);

        var loaded = _repository.GetAll().Single(p => p.Id == person.Id);
        Assert.False(loaded.IsArchived);
    }

    [Fact]
    public void GetAll_liefert_Personen_in_Erstellungsreihenfolge()
    {
        // Keine alphabetische Sortierung mehr, sondern SortOrder: die
        // zuerst angelegte Person steht zuerst, unabhaengig vom Namen -
        // erst MoveUp/MoveDown aendern das (siehe unten).
        _repository.Create("Bernd");
        _repository.Create("Anna");

        var names = _repository.GetAll().Select(p => p.Name).ToList();

        Assert.Equal(new[] { "Bernd", "Anna" }, names);
    }

    [Fact]
    public void MoveDown_und_MoveUp_vertauschen_SortOrder_mit_Nachbarn()
    {
        var erste = _repository.Create("Erste");
        _repository.Create("Zweite");

        _repository.MoveDown(erste.Id);
        var nachUnten = _repository.GetAll();
        Assert.Equal("Zweite", nachUnten[0].Name);
        Assert.Equal("Erste", nachUnten[1].Name);

        _repository.MoveUp(erste.Id);
        var nachOben = _repository.GetAll();
        Assert.Equal("Erste", nachOben[0].Name);
        Assert.Equal("Zweite", nachOben[1].Name);
    }

    [Fact]
    public void MoveUp_am_Anfang_der_Liste_aendert_nichts()
    {
        var erste = _repository.Create("Erste");
        _repository.Create("Zweite");

        _repository.MoveUp(erste.Id);

        Assert.Equal("Erste", _repository.GetAll()[0].Name);
    }

    [Fact]
    public void MoveDown_am_Ende_der_Liste_aendert_nichts()
    {
        _repository.Create("Erste");
        var zweite = _repository.Create("Zweite");

        _repository.MoveDown(zweite.Id);

        Assert.Equal("Zweite", _repository.GetAll()[1].Name);
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

    [Fact]
    public void GetExpenseCounts_zaehlt_Ausgaben_je_Zahler()
    {
        var categoryId = new CategoryRepository(_connection).Create("Pferde", null).Id;
        var expenses = new ExpenseRepository(_connection);
        var anna = _repository.Create("Anna");
        var bernd = _repository.Create("Bernd");

        expenses.Create(categoryId, 100, DateOnly.FromDateTime(DateTime.Now), anna.Id);
        expenses.Create(categoryId, 200, DateOnly.FromDateTime(DateTime.Now), anna.Id);

        var counts = _repository.GetExpenseCounts();

        Assert.Equal(2, counts[anna.Id]);
        Assert.False(counts.ContainsKey(bernd.Id));
    }
}
